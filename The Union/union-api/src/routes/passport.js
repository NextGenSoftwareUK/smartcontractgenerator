/**
 * Passport route
 *
 * GET /api/passport/:avatarId
 *
 * Assembles the Fighter Passport from OASIS data:
 *  - Avatar details (name, tier/level, karma = tokens)
 *  - Karma history (= session + quest token log)
 *  - Quest progress (from STAR quests API)
 */

const router = require('express').Router();
const oasis = require('../lib/oasis');

router.get('/:avatarId', async (req, res, next) => {
  try {
    const { avatarId } = req.params;

    const [avatar, karma, quests] = await Promise.allSettled([
      oasis.getAvatar(avatarId),
      oasis.getKarma(avatarId),
      oasis.listQuests()
    ]);

    const avatarData = avatar.status === 'fulfilled' ? avatar.value : null;
    const karmaData  = karma.status  === 'fulfilled' ? karma.value  : null;
    const questData  = quests.status === 'fulfilled' ? quests.value : [];

    res.json({
      avatarId,
      name: avatarData?.username ?? avatarData?.firstName ?? 'Unknown',
      karma: karmaData?.karma ?? 0,
      karmaHistory: karmaData?.karmaHistory ?? [],
      quests: questData?.quests ?? questData ?? [],
      // Raw OASIS data for debugging
      _avatar: avatarData
    });
  } catch (err) { next(err); }
});

module.exports = router;
