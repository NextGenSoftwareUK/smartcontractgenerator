/**
 * ZKTeco PUSH receiver
 *
 * Configure the ZKTeco device's "PUSH" / ADMS settings to:
 *   Server Address: <your-server-ip>
 *   Server Port:    3001
 *   URI:            /api/bridge/zkteco
 *
 * Most ZKTeco firmware (9.x+) will POST an XML or JSON payload to this URL
 * whenever a finger is scanned. This route normalises the payload and calls
 * processCheckin() → OASIS API.
 */

const router = require('express').Router();
const express = require('express');
const { processCheckin } = require('../lib/checkin');

// ZKTeco devices may send XML — accept text body too
router.use(express.text({ type: ['text/xml', 'application/xml'] }));

// Health check — ZKTeco pings this before sending events
router.get('/zkteco', (_req, res) => res.send('OK'));

// Main push endpoint
router.post('/zkteco', async (req, res, next) => {
  try {
    const raw = req.body;
    const records = parseZKPayload(raw);

    if (!records.length) return res.status(200).send('OK');

    const results = [];
    for (const { fingerprintId, checkinAt } of records) {
      const result = await processCheckin({
        fingerprintId,
        chapter: process.env.DEFAULT_CHAPTER ?? 'PDC',
        source: 'push'
      });
      results.push({ fingerprintId, ...result });
    }

    res.json({ processed: records.length, results });
  } catch (err) { next(err); }
});

/**
 * Parse ZKTeco push payload.
 * ZKTeco firmware varies — this handles the two most common formats.
 */
function parseZKPayload(raw) {
  if (!raw) return [];

  // JSON array format (newer firmware)
  if (Array.isArray(raw)) {
    return raw.map(r => ({
      fingerprintId: String(r.UserID ?? r.userId ?? r.id ?? ''),
      checkinAt: r.Time ? new Date(r.Time) : new Date()
    })).filter(r => r.fingerprintId);
  }

  // Single JSON object
  if (typeof raw === 'object' && raw.UserID) {
    return [{ fingerprintId: String(raw.UserID), checkinAt: raw.Time ? new Date(raw.Time) : new Date() }];
  }

  // XML string (older firmware) — simple regex parse
  if (typeof raw === 'string') {
    const userIds = [...raw.matchAll(/<UserID>(\d+)<\/UserID>/gi)].map(m => m[1]);
    const times   = [...raw.matchAll(/<Time>(.*?)<\/Time>/gi)].map(m => m[1]);
    return userIds.map((id, i) => ({
      fingerprintId: id,
      checkinAt: times[i] ? new Date(times[i]) : new Date()
    }));
  }

  return [];
}

module.exports = router;
