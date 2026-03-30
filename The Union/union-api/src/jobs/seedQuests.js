/**
 * Seed The Union quests into STAR.
 *
 * Run once: node src/jobs/seedQuests.js
 *
 * Prints the quest IDs — copy them into .env as QUEST_ID_10_SESSIONS, etc.
 * so the checkin service can call completeQuest() when milestones are hit.
 */

require('dotenv').config();
const oasis = require('../lib/oasis');

const QUESTS = [
  {
    key: 'QUEST_ID_10_SESSIONS',
    name: 'First 10 Sessions',
    description: 'Complete your first 10 training sessions at The Union. The hardest part is showing up.'
  },
  {
    key: 'QUEST_ID_50_SESSIONS',
    name: '50 Sessions — Fighter Status',
    description: 'Reach 50 sessions. You\'re no longer a newcomer — you\'re a fighter.'
  },
  {
    key: 'QUEST_ID_100_SESSIONS',
    name: '100 Sessions',
    description: 'A hundred hours on the mats. Your passport reflects your commitment.'
  },
  {
    key: 'QUEST_ID_200_SESSIONS',
    name: '200 Sessions — Veteran',
    description: 'Two hundred sessions. Veteran tier unlocked — priority booking and exclusive drops.'
  },
  {
    key: 'QUEST_ID_500_SESSIONS',
    name: '500 Sessions — Legend',
    description: 'The highest honour. 500 sessions recorded. Franchise eligibility and trainer pathway open.'
  },
  {
    key: 'QUEST_ID_CROSS_CHAPTER',
    name: 'Chapter Hopper',
    description: 'Train at two different Union chapters in the same month. Earn the Riviera stamp.'
  },
  {
    key: 'QUEST_ID_REVIEW',
    name: 'Spread the Word',
    description: 'Leave a Google or TripAdvisor review for The Union PDC.'
  },
  {
    key: 'QUEST_ID_REFERRAL',
    name: 'Bring a Fighter',
    description: 'Refer a member who trains for 3+ months.'
  }
];

async function seed() {
  console.log('Seeding Union quests into STAR...\n');

  const envLines = [];

  for (const q of QUESTS) {
    try {
      const result = await oasis.createQuest({ name: q.name, description: q.description });
      const id = result?.result?.id ?? result?.id ?? JSON.stringify(result);
      console.log(`✅ ${q.name}\n   ID: ${id}\n   .env: ${q.key}=${id}\n`);
      envLines.push(`${q.key}=${id}`);
    } catch (err) {
      console.error(`❌ Failed to create "${q.name}": ${err.message}`);
    }
  }

  console.log('\n── Copy these into your .env ──');
  console.log(envLines.join('\n'));
}

seed().catch(err => {
  console.error('Seed failed:', err.message);
  process.exit(1);
});
