/**
 * Session routes
 *
 * POST /api/sessions/fingerprint  — log check-in by fingerprintId (from bridge)
 * POST /api/sessions/manual       — log check-in by avatarId (admin fallback)
 * GET  /api/sessions/:avatarId    — fetch session holons for an avatar from OASIS
 */

const router = require('express').Router();
const { processCheckin } = require('../lib/checkin');
const oasis = require('../lib/oasis');

// Called by the ZKTeco bridge or push receiver
router.post('/fingerprint', async (req, res, next) => {
  try {
    const { fingerprintId, chapter, source = 'fingerprint' } = req.body;
    if (!fingerprintId) return res.status(400).json({ error: 'fingerprintId required' });
    const result = await processCheckin({ fingerprintId, chapter, source });
    res.json(result);
  } catch (err) { next(err); }
});

// Manual log (Jefferson / Mike admin panel)
router.post('/manual', async (req, res, next) => {
  try {
    const { avatarId, chapter = 'PDC' } = req.body;
    if (!avatarId) return res.status(400).json({ error: 'avatarId required' });
    const result = await processCheckin({ avatarId, chapter, source: 'manual' });
    res.json(result);
  } catch (err) { next(err); }
});

// Retrieve session history from OASIS holons
router.get('/:avatarId', async (req, res, next) => {
  try {
    // Load all holons — filter client-side for this avatar's CheckIn holons
    // In production, use the avatar's parentHolonId or search API
    const holons = await oasis.searchHolons({ holonType: 'All' });
    const sessions = (holons?.result ?? []).filter(h =>
      h.metadata?.type === 'CheckIn' && h.metadata?.avatarId === req.params.avatarId
    );
    res.json(sessions);
  } catch (err) { next(err); }
});

module.exports = router;
