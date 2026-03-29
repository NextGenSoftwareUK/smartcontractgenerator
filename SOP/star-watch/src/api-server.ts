import express from 'express'
import cors from 'cors'
import { getEvents, getMatches, getStatus } from './event-store'
import { log } from './logger'

// ─── Local HTTP API ───────────────────────────────────────────────────────
// Runs on port 3001 so the SOP app can poll for live activity.
// Only binds to localhost — not exposed externally.

const PORT = 3001

export function startApiServer(): void {
  const app = express()
  app.use(cors({ origin: '*' }))
  app.use(express.json())

  // Status — connectors, uptime, counts
  app.get('/status', (_req, res) => {
    res.json(getStatus())
  })

  // Recent raw events from all connectors
  app.get('/events', (req, res) => {
    const limit = Number(req.query.limit) || 20
    res.json(getEvents(limit))
  })

  // Matched events — events that triggered an SOP step
  app.get('/matches', (req, res) => {
    const limit = Number(req.query.limit) || 20
    res.json(getMatches(limit))
  })

  app.listen(PORT, '127.0.0.1', () => {
    log.success(`STAR Watch API running at http://localhost:${PORT}`)
  })
}
