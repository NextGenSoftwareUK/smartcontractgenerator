import { Connection } from 'jsforce'
import { readConfig } from '../config'
import { log } from '../logger'
import type { ObservedEvent } from '../types'

// ─── Salesforce Watcher ───────────────────────────────────────────────────
//
// Auth priority:
//   1. accessToken (from `sf org login web` via Salesforce CLI) — preferred
//   2. username + password + securityToken (SOAP login) — fallback
//
// Subscribes to the Salesforce Streaming API via CometD for each configured
// object. Falls back to REST polling if streaming cannot be established.

type EventCallback = (event: ObservedEvent) => void

interface SFConfig {
  instanceUrl:   string
  username:      string
  accessToken?:  string       // Set by `sf org login web` — preferred auth
  password?:     string       // Salesforce login password (SOAP fallback)
  securityToken?: string      // Appended to password for SOAP auth
  objects:       string[]
}

const POLL_INTERVAL_MS  = 8_000
const SF_API_VERSION    = 'v60.0'

// ─── Entry point ─────────────────────────────────────────────────────────

export async function startSalesforceWatcher(emit: EventCallback): Promise<void> {
  const config   = readConfig()
  const sfConfig = config.connectors?.salesforce as Partial<SFConfig> | undefined

  if (!sfConfig?.instanceUrl || !sfConfig?.username) {
    log.warn('Salesforce connector not configured. Run `star connect salesforce` to connect.')
    return
  }

  const sf: SFConfig = {
    instanceUrl:   sfConfig.instanceUrl,
    username:      sfConfig.username,
    accessToken:   sfConfig.accessToken as string | undefined,
    password:      sfConfig.password as string | undefined,
    securityToken: sfConfig.securityToken as string | undefined ?? '',
    objects:       (sfConfig.objects as string[] | undefined) ?? ['Opportunity', 'Case'],
  }

  log.info(`Salesforce watcher authenticating as ${sf.username}…`)

  try {
    let conn: Connection

    if (sf.accessToken) {
      // ── OAuth token from `sf org login web` — no SOAP needed ──────────
      conn = new Connection({ instanceUrl: sf.instanceUrl, accessToken: sf.accessToken })
      log.success(`Salesforce connected via OAuth token (${sf.instanceUrl})`)
    } else if (sf.password && sf.password !== '__NEEDS_SF_PASSWORD__') {
      // ── SOAP username+password fallback ───────────────────────────────
      conn = new Connection({ loginUrl: sf.instanceUrl })
      await conn.login(sf.username, sf.password + (sf.securityToken ?? ''))
      log.success(`Salesforce connected via SOAP login (${sf.instanceUrl})`)
    } else {
      log.warn('Salesforce: no accessToken or password configured — skipping.')
      return
    }
    log.info(`Watching objects: ${sf.objects.join(', ')}`)

    // Use polling for reliable event detection (works in all environments).
    // Streaming API (CometD) will be enabled as an opt-in once we confirm
    // the CometD keep-alive works reliably in the daemon runtime.
    startPolling(conn, sf, emit)
  } catch (err) {
    log.error(`Salesforce auth failed: ${err}`)
    log.warn('Check your password + security token in ~/.star/config.json')
  }
}

// ─── Streaming API (CometD/Bayeux) ───────────────────────────────────────

async function tryStreaming(
  conn:  Connection,
  sf:    SFConfig,
  emit:  EventCallback,
): Promise<boolean> {
  try {
    for (const objectType of sf.objects) {
      const topic = topicName(objectType)

      // Ensure the PushTopic exists — create it if not
      await ensurePushTopic(conn, topic, objectType)

      // Subscribe to the streaming channel
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      conn.streaming.topic(topic).subscribe((message: any) => {
        const event = normaliseSFStreamEvent(message as SFStreamMessage, objectType, sf.instanceUrl)
        if (event) emit(event)
      })

      log.success(`Subscribed to Salesforce streaming: /topic/${topic}`)
    }
    return true
  } catch (err) {
    log.warn(`Streaming setup failed (${err}) — will use polling`)
    return false
  }
}

async function ensurePushTopic(
  conn:       Connection,
  topic:      string,
  objectType: string,
): Promise<void> {
  try {
    const existing = await conn.query<{ Id: string }>(
      `SELECT Id FROM PushTopic WHERE Name = '${topic}' LIMIT 1`
    )
    if (existing.totalSize > 0) return

      const fields    = buildSOQLFields(objectType)
    const pushTopic = {
      Name:                  topic,
      Query:                 `SELECT ${fields} FROM ${objectType}`,
      ApiVersion:            60.0,
      NotifyForOperationCreate:  true,
      NotifyForOperationUpdate:  true,
      NotifyForOperationDelete:  false,
      NotifyForFields:           'Referenced',
    }

    await conn.sobject('PushTopic').create(pushTopic as Record<string, unknown>)
    log.info(`Created Salesforce PushTopic: ${topic}`)
  } catch (err) {
    // PushTopic creation requires admin permissions — log and continue
    log.warn(`Could not create PushTopic ${topic}: ${err}`)
  }
}

// ─── Polling fallback ─────────────────────────────────────────────────────

function startPolling(
  conn:  Connection,
  sf:    SFConfig,
  emit:  EventCallback,
): void {
  const lastSeen = new Map<string, string>()

  async function poll() {
    for (const objectType of sf.objects) {
      try {
        const since  = lastSeen.get(objectType) ?? new Date(Date.now() - POLL_INTERVAL_MS * 2).toISOString()
        const fields = buildSOQLFields(objectType)
        const soql   = `SELECT ${fields} FROM ${objectType} WHERE LastModifiedDate > ${since} ORDER BY LastModifiedDate DESC LIMIT 50`

        const result = await conn.query<SFRecord>(soql)

        for (const record of result.records) {
          emit(normaliseSFRecord(record, objectType, sf.instanceUrl))
          const ts = record.LastModifiedDate
          if (!lastSeen.has(objectType) || ts > lastSeen.get(objectType)!)
            lastSeen.set(objectType, ts)
        }
      } catch (err) {
        log.error(`Salesforce poll error for ${objectType}: ${err}`)
      }
    }

    setTimeout(poll, POLL_INTERVAL_MS)
  }

  poll()
  log.info(`Salesforce polling started (${sf.objects.join(', ')} every ${POLL_INTERVAL_MS / 1000}s)`)
}

// ─── Helpers ──────────────────────────────────────────────────────────────

// Salesforce PushTopic names are max 25 chars
function topicName(objectType: string): string {
  const map: Record<string, string> = {
    Opportunity: 'STARWatch_Oppty',
    Lead:        'STARWatch_Lead',
    Case:        'STARWatch_Case',
    Task:        'STARWatch_Task',
  }
  return map[objectType] ?? `STARWatch_${objectType}`.slice(0, 25)
}

function buildSOQLFields(objectType: string): string {
  // Case and Task don't have a 'Name' field — use object-specific fields
  switch (objectType) {
    case 'Opportunity': return 'Id, Name, StageName, Amount, CloseDate, AccountId, LastModifiedDate, LastModifiedById, OwnerId'
    case 'Case':        return 'Id, Subject, Status, Priority, AccountId, LastModifiedDate, LastModifiedById, OwnerId'
    case 'Lead':        return 'Id, Name, Status, Company, Email, LastModifiedDate, LastModifiedById, OwnerId'
    case 'Task':        return 'Id, Subject, Status, ActivityDate, LastModifiedDate, LastModifiedById, OwnerId'
    default:            return 'Id, Name, LastModifiedDate, LastModifiedById, OwnerId'
  }
}

// ─── Event normalisation ──────────────────────────────────────────────────

interface SFRecord {
  Id:               string
  Name?:            string
  StageName?:       string
  Status?:          string
  Priority?:        string
  Subject?:         string
  OwnerId?:         string
  LastModifiedDate: string
  LastModifiedById?: string
  [key: string]:    unknown
}

interface SFStreamMessage {
  channel: string
  data: {
    event:  { type: string; createdDate: string; replayId: number }
    sobject: SFRecord
  }
}

function normaliseSFStreamEvent(
  msg:         SFStreamMessage,
  objectType:  string,
  instanceUrl: string,
): ObservedEvent | null {
  const record  = msg.data?.sobject
  if (!record?.Id) return null
  return normaliseSFRecord(
    { ...record, LastModifiedDate: msg.data.event.createdDate },
    objectType,
    instanceUrl,
  )
}

function normaliseSFRecord(
  record:      SFRecord,
  objectType:  string,
  instanceUrl: string,
): ObservedEvent {
  const displayName = record.Name ?? record.Subject ?? record.Id
  const contextParts: string[] = [`${objectType} "${displayName}" was modified`]
  if (record.StageName) contextParts.push(`Stage: ${record.StageName}`)
  if (record.Status)    contextParts.push(`Status: ${record.Status}`)
  if (record.Priority)  contextParts.push(`Priority: ${record.Priority}`)
  if (record.Subject)   contextParts.push(`Subject: ${record.Subject}`)

  return {
    id:        `sf-${record.Id}-${record.LastModifiedDate}`,
    source:    'salesforce',
    timestamp: new Date(record.LastModifiedDate),
    actor: {
      id:   record.LastModifiedById ?? record.OwnerId ?? 'system',
      name: record.OwnerId ?? 'Salesforce System',
    },
    action:    inferSFAction(record, objectType),
    entity: {
      type: objectType.toLowerCase(),
      id:   record.Id,
      name: record.Name ?? record.Subject,
      url:  `${instanceUrl}/lightning/r/${objectType}/${record.Id}/view`,
    },
    payload: record as unknown as Record<string, unknown>,
    context: contextParts,
  }
}

function inferSFAction(record: SFRecord, objectType: string): string {
  if (objectType === 'Opportunity' && record.StageName)
    return `stage_changed_to_${record.StageName.toLowerCase().replace(/\s+/g, '_')}`
  if (objectType === 'Case' && record.Status)
    return `status_changed_to_${record.Status.toLowerCase().replace(/\s+/g, '_')}`
  if (objectType === 'Lead' && record.Status)
    return `lead_status_${record.Status.toLowerCase().replace(/\s+/g, '_')}`
  return `${objectType.toLowerCase()}_updated`
}
