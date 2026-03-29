import { Connection } from 'jsforce'
import { readConfig } from '../config'
import { log } from '../logger'
import type { ObservedEvent } from '../types'

// ─── Salesforce Watcher ───────────────────────────────────────────────────
//
// Authenticates with username + password + security token (no Connected App
// required for this flow).  Subscribes to the Salesforce Streaming API via
// CometD (jsforce handles the Bayeux handshake) for each configured object.
// Falls back to REST polling if streaming cannot be established.

type EventCallback = (event: ObservedEvent) => void

interface SFConfig {
  instanceUrl:   string
  username:      string
  password:      string       // Salesforce login password
  securityToken: string       // Appended to password for API auth
  objects:       string[]     // e.g. ['Opportunity', 'Lead', 'Case', 'Task']
}

const POLL_INTERVAL_MS  = 15_000
const SF_API_VERSION    = 'v60.0'

// ─── Entry point ─────────────────────────────────────────────────────────

export async function startSalesforceWatcher(emit: EventCallback): Promise<void> {
  const config   = readConfig()
  const sfConfig = config.connectors?.salesforce as Partial<SFConfig> | undefined

  if (!sfConfig?.instanceUrl || !sfConfig?.username) {
    log.warn('Salesforce connector not configured. Run `star connect salesforce` to connect.')
    return
  }

  if (sfConfig.password === '__NEEDS_SF_PASSWORD__' || !sfConfig.password) {
    log.warn('Salesforce password not yet set in ~/.star/config.json — skipping Salesforce watcher.')
    return
  }

  const sf: SFConfig = {
    instanceUrl:   sfConfig.instanceUrl,
    username:      sfConfig.username,
    password:      sfConfig.password,
    securityToken: sfConfig.securityToken ?? '',
    objects:       sfConfig.objects ?? ['Opportunity', 'Case'],
  }

  log.info(`Salesforce watcher authenticating as ${sf.username}…`)

  try {
    const conn = new Connection({ loginUrl: sf.instanceUrl })

    // jsforce requires password + securityToken concatenated
    await conn.login(sf.username, sf.password + sf.securityToken)

    log.success(`Salesforce connected (${sf.instanceUrl})`)
    log.info(`Watching objects: ${sf.objects.join(', ')}`)

    // Try Streaming API first — real-time CometD subscriptions
    const streamingOk = await tryStreaming(conn, sf, emit)

    // Fall back to polling if streaming setup fails
    if (!streamingOk) {
      log.warn('Streaming API unavailable — falling back to polling every 15s')
      startPolling(conn, sf, emit)
    }
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
      const topicName = `OASISStarWatch_${objectType}`

      // Ensure the PushTopic exists — create it if not
      await ensurePushTopic(conn, topicName, objectType)

      // Subscribe to the streaming channel
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      conn.streaming.topic(topicName).subscribe((message: any) => {
        const event = normaliseSFStreamEvent(message as SFStreamMessage, objectType, sf.instanceUrl)
        if (event) emit(event)
      })

      log.success(`Subscribed to Salesforce streaming: /topic/${topicName}`)
    }
    return true
  } catch (err) {
    log.warn(`Streaming setup failed (${err}) — will use polling`)
    return false
  }
}

async function ensurePushTopic(
  conn:       Connection,
  topicName:  string,
  objectType: string,
): Promise<void> {
  try {
    const existing = await conn.query<{ Id: string }>(
      `SELECT Id FROM PushTopic WHERE Name = '${topicName}' LIMIT 1`
    )
    if (existing.totalSize > 0) return

    const fields    = buildSOQLFields(objectType)
    const pushTopic = {
      Name:                  topicName,
      Query:                 `SELECT ${fields} FROM ${objectType}`,
      ApiVersion:            60.0,
      NotifyForOperationCreate:  true,
      NotifyForOperationUpdate:  true,
      NotifyForOperationDelete:  false,
      NotifyForFields:           'Referenced',
    }

    await conn.sobject('PushTopic').create(pushTopic as Record<string, unknown>)
    log.info(`Created Salesforce PushTopic: ${topicName}`)
  } catch (err) {
    // PushTopic creation requires admin permissions — log and continue
    log.warn(`Could not create PushTopic ${topicName}: ${err}`)
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

function buildSOQLFields(objectType: string): string {
  const base = 'Id, Name, LastModifiedDate, LastModifiedById, OwnerId'
  const extra =
    objectType === 'Opportunity' ? ', StageName, Amount, CloseDate, AccountId' :
    objectType === 'Case'        ? ', Status, Priority, Subject, AccountId' :
    objectType === 'Lead'        ? ', Status, Company, Email' :
    objectType === 'Task'        ? ', Status, Subject, ActivityDate' : ''
  return base + extra
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
  const contextParts: string[] = [`${objectType} "${record.Name ?? record.Id}" was modified`]
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
      name: record.Name,
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
