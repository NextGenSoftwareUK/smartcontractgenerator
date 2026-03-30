/**
 * Notifier — sends WhatsApp / SMS when a member checks in.
 *
 * Phase 1: console log (always works, zero cost)
 * Phase 2: configure WHATSAPP_WEBHOOK_URL to post to a n8n/Make webhook
 *          which sends a WhatsApp message via the Meta Business API or Twilio.
 *
 * Message format:
 *   "Welcome back, Carlos! ✅ +10 karma. You're on 47 sessions. Keep going! 🐯"
 */

const axios = require('axios');

async function sendNotification({ avatarId, memberName, chapter, completedQuests = [], karmaResult }) {
  const questLines = completedQuests.length
    ? `\n🏆 Quest unlocked: ${completedQuests.join(', ')}!`
    : '';

  const message = `Welcome back, ${memberName}! ✅ Session logged at The Union ${chapter}.${questLines} 🐯`;

  console.log(`[notifier] ${message}`);

  const webhookUrl = process.env.WHATSAPP_WEBHOOK_URL;
  if (!webhookUrl) return; // Phase 1: log only

  try {
    await axios.post(webhookUrl, {
      avatarId,
      memberName,
      chapter,
      message,
      completedQuests
    });
  } catch (err) {
    console.error('[notifier] Webhook failed:', err.message);
  }
}

module.exports = { sendNotification };
