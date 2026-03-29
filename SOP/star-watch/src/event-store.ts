import type { ObservedEvent, MatchResult } from './types'

// ─── Event Store ─────────────────────────────────────────────────────────
// Maintains in-memory circular buffers of recent events and match results.
// Exposed via the local HTTP API so the SOP app can display a live feed.

const MAX_EVENTS  = 100
const MAX_MATCHES = 50

export interface StoredEvent {
  id:        string
  timestamp: string
  source:    string
  action:    string
  actor:     string
  entity:    string
  entityUrl?: string
  context:   string
}

export interface StoredMatch {
  id:          string
  timestamp:   string
  source:      string
  action:      string
  actor:       string
  stepName:    string
  sopName:     string
  confidence:  number
  matchAction: string
  reasoning:   string
}

const events:  StoredEvent[]  = []
const matches: StoredMatch[]  = []
let startedAt = new Date().toISOString()

export function recordEvent(event: ObservedEvent): void {
  events.unshift({
    id:        event.id,
    timestamp: event.timestamp.toISOString(),
    source:    event.source,
    action:    event.action,
    actor:     event.actor.name || event.actor.id,
    entity:    event.entity.name ?? event.entity.id,
    entityUrl: event.entity.url,
    context:   event.context[0] ?? '',
  })
  if (events.length > MAX_EVENTS) events.length = MAX_EVENTS
}

export function recordMatch(match: MatchResult): void {
  matches.unshift({
    id:          `${match.runId}-${match.stepId}-${Date.now()}`,
    timestamp:   new Date().toISOString(),
    source:      match.event.source,
    action:      match.event.action,
    actor:       match.event.actor.name || match.event.actor.id,
    stepName:    match.step.name,
    sopName:     match.run.sopName,
    confidence:  match.confidence,
    matchAction: match.action,
    reasoning:   match.reasoning,
  })
  if (matches.length > MAX_MATCHES) matches.length = MAX_MATCHES
}

export function getEvents(limit = 20)  { return events.slice(0, limit) }
export function getMatches(limit = 20) { return matches.slice(0, limit) }
export function getStatus() {
  return {
    startedAt,
    eventCount:  events.length,
    matchCount:  matches.length,
    connectors:  [...new Set(events.map(e => e.source))],
    lastEventAt: events[0]?.timestamp ?? null,
    lastMatchAt: matches[0]?.timestamp ?? null,
  }
}

export function resetStartedAt() { startedAt = new Date().toISOString() }
