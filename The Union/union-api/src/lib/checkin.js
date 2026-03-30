/**
 * processCheckin
 *
 * Called by both the ZKTeco push receiver and the manual session route.
 * All data lands in OASIS — no local DB writes beyond dedup.
 *
 * Flow:
 *  1. Look up avatarId from fingerprintId (local mapping)
 *  2. Dedup guard — ignore if already processed within 4 h
 *  3. Save a CheckInHolon in OASIS (permanent, verifiable record)
 *  4. Award karma (= Union tokens) via OASIS karma API
 *  5. Check session-count milestones and fire quest completions
 *  6. Send WhatsApp notification (optional)
 */

const oasis = require('./oasis');
const { getMemberByFingerprint, canAwardSession, recordSession } = require('./localDb');
const { sendNotification } = require('./notifier');

// Karma per session — maps to "10 tokens" in our design
const SESSION_KARMA_TYPE = 'BeInspiring'; // closest positive type for "showing up"

// Session milestones that trigger STAR quest completion
// questId must match the quest created by seedQuests.js
const SESSION_MILESTONES = [
  { sessions: 10,  questId: process.env.QUEST_ID_10_SESSIONS },
  { sessions: 50,  questId: process.env.QUEST_ID_50_SESSIONS },
  { sessions: 100, questId: process.env.QUEST_ID_100_SESSIONS },
  { sessions: 200, questId: process.env.QUEST_ID_200_SESSIONS },
  { sessions: 500, questId: process.env.QUEST_ID_500_SESSIONS }
];

async function processCheckin({ fingerprintId, avatarId: directAvatarId, chapter = 'PDC', source = 'fingerprint' }) {
  // 1. Resolve member
  let avatarId = directAvatarId;
  let memberName = 'Member';
  let memberPhone = null;

  if (!avatarId && fingerprintId) {
    const member = await getMemberByFingerprint(fingerprintId);
    if (!member) {
      return { skipped: true, reason: `No OASIS avatar linked to fingerprintId ${fingerprintId}` };
    }
    avatarId = member.avatarId;
    memberName = member.name;
    memberPhone = member.phone ?? null;
    chapter = member.chapter ?? chapter;
  }

  // 2. Dedup guard
  const key = fingerprintId ?? avatarId;
  const eligible = await canAwardSession(key);
  if (!eligible) {
    return { skipped: true, reason: 'Session already recorded within 4 hours' };
  }
  await recordSession(key);

  const checkinAt = new Date().toISOString();

  // 3. Save CheckInHolon in OASIS
  //    This is the permanent, verifiable session record.
  let holon;
  try {
    holon = await oasis.saveHolon({
      name: `CheckIn:${avatarId}:${checkinAt}`,
      description: `Training session at The Union ${chapter}`,
      holonType: 40,
      metadata: {
        type: 'CheckIn',
        avatarId,
        fingerprintId: fingerprintId ?? null,
        chapter,
        source,
        checkinAt
      }
    });
  } catch (err) {
    console.error('[checkin] saveHolon failed:', err.message);
    // Non-fatal — we still award karma
  }

  // 4. Award karma (= tokens)
  let karmaResult;
  try {
    karmaResult = await oasis.addKarma({
      avatarId,
      karmaType: SESSION_KARMA_TYPE,
      description: `Training session at The Union ${chapter} · ${new Date(checkinAt).toLocaleDateString()}`
    });
  } catch (err) {
    console.error('[checkin] addKarma failed:', err.message);
  }

  // 5. Get current karma total to check milestones
  //    OASIS karma history tells us total accumulated karma = our session count proxy.
  //    We use holon metadata count in production; for now we use karma stats.
  let completedQuests = [];
  try {
    const karmaStats = await oasis.getKarma(avatarId);
    const totalKarma = karmaStats?.karma ?? 0;
    // Each session = 1 karma unit, so totalKarma ≈ totalSessions
    const approxSessions = totalKarma;

    for (const milestone of SESSION_MILESTONES) {
      if (milestone.questId && approxSessions >= milestone.sessions) {
        try {
          await oasis.completeQuest({ questId: milestone.questId, avatarId });
          completedQuests.push(milestone);
          console.log(`[checkin] Quest completed: ${milestone.sessions} sessions for avatar ${avatarId}`);
        } catch (err) {
          // Quest may already be completed — ignore duplicate errors
          if (!err.message?.includes('already')) {
            console.warn(`[checkin] completeQuest failed:`, err.message);
          }
        }
      }
    }
  } catch (err) {
    console.warn('[checkin] karma check failed:', err.message);
  }

  // 6. Notify via WhatsApp
  try {
    const karmaStats = karmaResult ?? {};
    await sendNotification({
      memberName,
      phone: memberPhone,
      chapter,
      karmaEarned: SESSION_TOKENS,
      karmaTotal: karmaStats?.karma ?? SESSION_TOKENS,
      totalSessions: member?.totalSessions ?? 1,
      completedQuests: completedQuests.map(q => q.sessions + ' Sessions'),
      tier: 'recruit'
    });
  } catch (err) {
    console.warn('[checkin] notification failed:', err.message);
  }

  return {
    success: true,
    avatarId,
    chapter,
    checkinAt,
    holonId: holon?.result?.id ?? null,
    karmaAwarded: !!karmaResult,
    completedQuests: completedQuests.map(q => q.sessions + ' sessions')
  };
}

module.exports = { processCheckin };
