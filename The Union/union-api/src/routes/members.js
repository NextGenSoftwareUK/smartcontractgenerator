/**
 * Member routes
 *
 * POST /api/members/register  — create OASIS avatar + link fingerprintId
 * POST /api/members/link      — link a fingerprintId to an existing avatarId
 * GET  /api/members           — list all linked members (local mapping)
 * GET  /api/members/:avatarId — full OASIS avatar details
 */

const router = require('express').Router();
const oasis = require('../lib/oasis');
const { linkFingerprint, getMemberByFingerprint, getAllMembers } = require('../lib/localDb');

// Register a new member — creates their OASIS avatar + stores fingerprint link
router.post('/register', async (req, res, next) => {
  try {
    const { fingerprintId, username, email, password, firstName, lastName, phone, chapter = 'PDC' } = req.body;

    if (!username || !email || !password) {
      return res.status(400).json({ error: 'username, email, password required' });
    }

    // 1. Create OASIS avatar
    const avatar = await oasis.registerAvatar({ username, email, password, firstName, lastName });

    // 2. Store local fingerprint → avatarId mapping (if device ID provided)
    if (fingerprintId) {
      await linkFingerprint({
        fingerprintId,
        avatarId: avatar.id ?? avatar.avatarId,
        name: `${firstName ?? ''} ${lastName ?? ''}`.trim() || username,
        phone: phone ?? null,
        chapter
      });
    }

    res.status(201).json({
      message: 'Avatar registered. Ask member to verify their email before first check-in.',
      avatarId: avatar.id ?? avatar.avatarId,
      fingerprintLinked: !!fingerprintId
    });
  } catch (err) { next(err); }
});

// Link a fingerprintId to an existing OASIS avatar
router.post('/link', async (req, res, next) => {
  try {
    const { fingerprintId, avatarId, name, phone, chapter = 'PDC' } = req.body;
    if (!fingerprintId || !avatarId) {
      return res.status(400).json({ error: 'fingerprintId and avatarId required' });
    }
    await linkFingerprint({ fingerprintId, avatarId, name: name ?? avatarId, phone: phone ?? null, chapter });
    res.json({ message: 'Fingerprint linked', fingerprintId, avatarId });
  } catch (err) { next(err); }
});

// List all locally-known members
router.get('/', async (_req, res, next) => {
  try {
    const members = await getAllMembers();
    res.json(members);
  } catch (err) { next(err); }
});

// Get full OASIS avatar for a member
router.get('/:avatarId', async (req, res, next) => {
  try {
    const avatar = await oasis.getAvatar(req.params.avatarId);
    res.json(avatar);
  } catch (err) { next(err); }
});

module.exports = router;
