/**
 * WhatsApp notifier — Meta Cloud API (free)
 *
 * Calls graph.facebook.com directly. No third-party service, no monthly fee.
 * Free tier: 1,000 service conversations/month — more than enough for a gym.
 *
 * Docs: https://developers.facebook.com/docs/whatsapp/cloud-api/messages
 *
 * Required env vars (Jefferson sets these up):
 *   WHATSAPP_PHONE_NUMBER_ID   — from Meta developer dashboard
 *   WHATSAPP_ACCESS_TOKEN      — from Meta developer dashboard
 *
 * If env vars are not set, falls back to console log (Phase 1 / local dev).
 */

const axios = require('axios');

const META_API = 'https://graph.facebook.com/v19.0';

// ─── Send a WhatsApp message ───────────────────────────────────────────────

async function sendWhatsApp(to, message) {
  const phoneNumberId = process.env.WHATSAPP_PHONE_NUMBER_ID;
  const token = process.env.WHATSAPP_ACCESS_TOKEN;

  if (!phoneNumberId || !token) {
    console.log(`[whatsapp → ${to}] ${message}`);
    return;
  }

  // Normalise phone number — remove spaces, ensure + prefix
  const phone = to.replace(/\s+/g, '').replace(/^00/, '+');

  try {
    await axios.post(
      `${META_API}/${phoneNumberId}/messages`,
      {
        messaging_product: 'whatsapp',
        recipient_type: 'individual',
        to: phone,
        type: 'text',
        text: { preview_url: false, body: message }
      },
      { headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' } }
    );
  } catch (err) {
    const detail = err.response?.data?.error?.message ?? err.message;
    console.error(`[whatsapp] Failed to send to ${to}: ${detail}`);
  }
}

// ─── Message templates ────────────────────────────────────────────────────

function checkinMessage({ memberName, chapter, karmaEarned, karmaTotal, totalSessions, completedQuests }) {
  const questLine = completedQuests?.length
    ? `\n\n🏆 *Quest unlocked:* ${completedQuests.join(', ')}!`
    : '';

  const nextMilestone = nextSessionMilestone(totalSessions);
  const progressLine = nextMilestone
    ? `\n_${nextMilestone.away} sessions until your next quest (${nextMilestone.name})_`
    : `\n_You've hit every session milestone. You're a Legend. 🐯_`;

  return (
    `✅ *Session logged — The Union ${chapter}*\n\n` +
    `Welcome back, ${memberName}!\n` +
    `You've earned *+${karmaEarned} karma* 🐯\n` +
    `Total balance: *${karmaTotal} karma*` +
    questLine +
    progressLine +
    `\n\nReply *BALANCE* · *PASSPORT* · *QUESTS* · *DISCOUNT*`
  );
}

function balanceMessage({ memberName, karmaTotal, tier }) {
  const TIERS = {
    recruit: { label: 'Recruit 🥊', next: 'Fighter', sessionsNeeded: 50 },
    fighter: { label: 'Fighter ⚔️', next: 'Veteran', sessionsNeeded: 200 },
    veteran: { label: 'Veteran 🦁', next: 'Legend', sessionsNeeded: 500 },
    legend:  { label: 'Legend 🐯', next: null, sessionsNeeded: null }
  };

  const tierInfo = TIERS[tier] ?? TIERS.recruit;
  const discountLine = karmaTotal >= 500
    ? `\n💸 Reply *DISCOUNT* to claim your 20% store discount.`
    : `\n_${500 - karmaTotal} more karma for a 20% store discount._`;

  return (
    `🐯 *${memberName}'s Balance*\n\n` +
    `Karma: *${karmaTotal}*\n` +
    `Tier: *${tierInfo.label}*` +
    (tierInfo.next ? `\n_${tierInfo.sessionsNeeded} sessions to reach ${tierInfo.next}_` : '') +
    discountLine
  );
}

function passportMessage({ memberName, tier, totalSessions, karmaTotal, chapters, completedQuestCount, totalQuestCount }) {
  const chapterList = chapters?.length
    ? chapters.map(c => `  • ${c.chapter} — ${c.sessionCount} sessions`).join('\n')
    : '  • PDC (home chapter)';

  return (
    `📜 *Fighter Passport — ${memberName}*\n\n` +
    `Tier: *${tier.charAt(0).toUpperCase() + tier.slice(1)} 🐯*\n` +
    `Sessions: *${totalSessions}*\n` +
    `Karma: *${karmaTotal}*\n` +
    `Quests: *${completedQuestCount}/${totalQuestCount}* completed\n\n` +
    `*Chapters visited:*\n${chapterList}\n\n` +
    `_Your passport is verified and portable — show it at any Union chapter worldwide._`
  );
}

function questsMessage({ memberName, quests }) {
  if (!quests?.length) return `No quests found, ${memberName}. Check back after your next session.`;

  const lines = quests.slice(0, 6).map(q => {
    const check = q.completed ? '✅' : '⬜';
    const progress = !q.completed && q.percentComplete > 0
      ? ` _(${q.percentComplete}%)_`
      : '';
    return `${check} ${q.name}${progress}`;
  });

  return `🎯 *${memberName}'s Quests*\n\n${lines.join('\n')}\n\n_Complete quests to earn bonus karma and unlock store discounts._`;
}

function discountMessage({ memberName, karmaTotal, discountPct, code }) {
  if (!code) {
    return (
      `💸 *Store Discount — ${memberName}*\n\n` +
      `You need *500 karma* for a 20% discount.\n` +
      `You have *${karmaTotal} karma* — ${500 - karmaTotal} more to go.\n\n` +
      `Keep training! 🐯`
    );
  }

  return (
    `🎉 *${discountPct}% Discount Unlocked!*\n\n` +
    `Use code *${code}* at checkout:\n` +
    `https://store.theunionpdc.com\n\n` +
    `_This code is single-use and valid for 7 days._`
  );
}

function questCompleteMessage({ memberName, questName, karmaEarned, newBalance }) {
  return (
    `🏆 *Quest Complete — ${memberName}*\n\n` +
    `*${questName}*\n\n` +
    `You've earned *+${karmaEarned} karma* 🐯\n` +
    `New balance: *${newBalance}*\n\n` +
    `Reply *PASSPORT* to see your full record.`
  );
}

// ─── Main notification dispatcher ────────────────────────────────────────

/**
 * Called by checkin.js after every fingerprint scan.
 * `member` must have a `phone` field in the local db or OASIS avatar.
 */
async function sendNotification({ memberName, phone, chapter, karmaEarned, karmaTotal,
  totalSessions, completedQuests, tier }) {
  if (!phone) {
    console.log(`[notifier] No phone for ${memberName} — skipping WhatsApp`);
    return;
  }

  const message = checkinMessage({
    memberName,
    chapter,
    karmaEarned: karmaEarned ?? 10,
    karmaTotal: karmaTotal ?? 0,
    totalSessions: totalSessions ?? 0,
    completedQuests
  });

  await sendWhatsApp(phone, message);
}

// ─── Helpers ──────────────────────────────────────────────────────────────

const SESSION_MILESTONES = [10, 50, 100, 200, 500];

function nextSessionMilestone(current) {
  const next = SESSION_MILESTONES.find(m => m > current);
  if (!next) return null;
  const NAMES = { 10: 'First 10', 50: 'Fighter Status', 100: '100 Sessions', 200: 'Veteran', 500: 'Legend' };
  return { sessions: next, name: NAMES[next], away: next - current };
}

module.exports = {
  sendNotification,
  sendWhatsApp,
  checkinMessage,
  balanceMessage,
  passportMessage,
  questsMessage,
  discountMessage,
  questCompleteMessage
};
