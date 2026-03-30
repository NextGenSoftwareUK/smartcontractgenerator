/**
 * WhatsApp inbound webhook — Meta Cloud API
 *
 * Meta calls this route when a member replies to a notification.
 *
 * GET  /api/bridge/whatsapp  — webhook verification (Meta calls this once on setup)
 * POST /api/bridge/whatsapp  — inbound messages from members
 *
 * Supported commands (case-insensitive):
 *   BALANCE   → karma total + tier + what it unlocks
 *   PASSPORT  → full fighter passport summary
 *   QUESTS    → quest progress list
 *   DISCOUNT  → generate store discount code if eligible
 *   HELP      → command list
 */

const router = require('express').Router();
const { getAllMembers } = require('../lib/localDb');
const oasis = require('../lib/oasis');
const {
  sendWhatsApp,
  balanceMessage,
  passportMessage,
  questsMessage,
  discountMessage
} = require('../lib/notifier');

// ─── Webhook verification (GET) ──────────────────────────────────────────────
// Meta sends a challenge when you first register the webhook URL.
router.get('/', (req, res) => {
  const mode      = req.query['hub.mode'];
  const token     = req.query['hub.verify_token'];
  const challenge = req.query['hub.challenge'];

  if (mode === 'subscribe' && token === process.env.WHATSAPP_VERIFY_TOKEN) {
    console.log('[whatsapp] Webhook verified');
    return res.status(200).send(challenge);
  }
  res.sendStatus(403);
});

// ─── Inbound messages (POST) ─────────────────────────────────────────────────
router.post('/', async (req, res) => {
  // Always acknowledge immediately — Meta will retry if we don't respond within 20s
  res.sendStatus(200);

  try {
    const body = req.body;
    if (body.object !== 'whatsapp_business_account') return;

    for (const entry of body.entry ?? []) {
      for (const change of entry.changes ?? []) {
        const messages = change.value?.messages;
        if (!messages?.length) continue;

        for (const msg of messages) {
          if (msg.type !== 'text') continue;

          const from    = msg.from;             // member's phone number
          const text    = msg.text?.body?.trim().toUpperCase();
          const msgId   = msg.id;

          console.log(`[whatsapp] Inbound from ${from}: "${text}"`);

          await handleCommand({ from, text });
        }
      }
    }
  } catch (err) {
    console.error('[whatsapp] Inbound error:', err.message);
  }
});

// ─── Command handler ─────────────────────────────────────────────────────────

async function handleCommand({ from, text }) {
  // Find member by phone number
  const members = await getAllMembers();
  const entry = Object.values(members).find(m => {
    const stored = (m.phone ?? '').replace(/\s+/g, '').replace(/^00/, '+');
    const inbound = from.startsWith('+') ? from : `+${from}`;
    return stored === inbound;
  });

  if (!entry) {
    await sendWhatsApp(from,
      `Hi! I don't recognise this number yet. 🐯\n\nAsk Jefferson or Mike at The Union PDC to enrol you in the passport system.`
    );
    return;
  }

  const { avatarId, name: memberName } = entry;

  switch (true) {
    case text === 'BALANCE' || text === 'BAL': {
      const [avatar, karma] = await Promise.allSettled([
        oasis.getAvatar(avatarId),
        oasis.getKarma(avatarId)
      ]);
      const tier      = avatar.value?.level ?? 'recruit';
      const karmaTotal = karma.value?.karma ?? 0;
      await sendWhatsApp(from, balanceMessage({ memberName, karmaTotal, tier }));
      break;
    }

    case text === 'PASSPORT' || text === 'PASS': {
      const [avatar, karma, quests] = await Promise.allSettled([
        oasis.getAvatar(avatarId),
        oasis.getKarma(avatarId),
        oasis.listQuests()
      ]);
      const tier             = avatar.value?.level ?? 'recruit';
      const karmaTotal       = karma.value?.karma ?? 0;
      const totalSessions    = avatar.value?.totalSessions ?? karmaTotal;
      const allQuests        = quests.value?.quests ?? [];
      const completedQuests  = avatar.value?.completedQuests ?? [];

      await sendWhatsApp(from, passportMessage({
        memberName,
        tier,
        totalSessions,
        karmaTotal,
        chapters: entry.chapters ?? [{ chapter: entry.chapter ?? 'PDC', sessionCount: totalSessions }],
        completedQuestCount: completedQuests.length,
        totalQuestCount: allQuests.length
      }));
      break;
    }

    case text === 'QUESTS' || text === 'QUEST': {
      const [karma, allQuests] = await Promise.allSettled([
        oasis.getKarma(avatarId),
        oasis.listQuests()
      ]);
      const karmaTotal   = karma.value?.karma ?? 0;
      const questList    = (allQuests.value?.quests ?? []).map(q => ({
        name: q.name,
        completed: false,      // TODO: cross-ref with avatar completedQuests
        percentComplete: 0
      }));
      await sendWhatsApp(from, questsMessage({ memberName, quests: questList }));
      break;
    }

    case text === 'DISCOUNT' || text === 'CODE': {
      const karma      = await oasis.getKarma(avatarId).catch(() => null);
      const karmaTotal = karma?.karma ?? 0;

      if (karmaTotal >= 500) {
        // Generate a simple one-time code — in production, create a Medusa discount
        const code = `UNION-${memberName.split(' ')[0].toUpperCase()}-20`;
        await sendWhatsApp(from, discountMessage({ memberName, karmaTotal, discountPct: 20, code }));
      } else if (karmaTotal >= 100) {
        const code = `UNION-${memberName.split(' ')[0].toUpperCase()}-5`;
        await sendWhatsApp(from, discountMessage({ memberName, karmaTotal, discountPct: 5, code }));
      } else {
        await sendWhatsApp(from, discountMessage({ memberName, karmaTotal, discountPct: null, code: null }));
      }
      break;
    }

    case text === 'HELP' || text === '?': {
      await sendWhatsApp(from,
        `🐯 *The Union PDC — Commands*\n\n` +
        `*BALANCE* — your karma total & tier\n` +
        `*PASSPORT* — your full fighter record\n` +
        `*QUESTS* — progress toward rewards\n` +
        `*DISCOUNT* — claim a store discount code\n\n` +
        `_Karma is earned automatically every time you train. Keep showing up._`
      );
      break;
    }

    default: {
      // Unknown command — gentle nudge
      await sendWhatsApp(from,
        `Hey ${memberName}! 🐯\n\nReply with: *BALANCE* · *PASSPORT* · *QUESTS* · *DISCOUNT* · *HELP*`
      );
    }
  }
}

module.exports = router;
