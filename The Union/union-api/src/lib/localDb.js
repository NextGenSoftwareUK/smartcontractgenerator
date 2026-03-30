/**
 * Lightweight local store (JSON file via lowdb).
 *
 * Only stores what OASIS cannot: the fingerprint device ID → OASIS avatarId mapping,
 * and a session dedup cache to prevent double-counting rapid re-scans.
 *
 * Everything else (quests, karma, sessions, passport) lives in OASIS.
 */

const { JSONFilePreset } = require('lowdb/node');
const path = require('path');

const DB_PATH = process.env.LOCAL_DB_PATH ?? path.join(__dirname, '../../data/db.json');

const DEFAULT = {
  // fingerprintId (string) → { avatarId, name, chapter }
  members: {},
  // key = `${fingerprintId}:${YYYY-MM-DD}` → ISO timestamp of last session awarded
  sessionDedup: {}
};

let db;

async function getDb() {
  if (!db) {
    db = await JSONFilePreset(DB_PATH, DEFAULT);
  }
  return db;
}

async function linkFingerprint({ fingerprintId, avatarId, name, phone, chapter = 'PDC' }) {
  const db = await getDb();
  db.data.members[fingerprintId] = { avatarId, name, phone: phone ?? null, chapter };
  await db.write();
}

async function getMemberByFingerprint(fingerprintId) {
  const db = await getDb();
  return db.data.members[fingerprintId] ?? null;
}

async function getAllMembers() {
  const db = await getDb();
  return db.data.members;
}

/**
 * Returns true if we should award a session token for this scan.
 * Prevents double-awarding if the device fires multiple events within 4 hours.
 */
async function canAwardSession(fingerprintId) {
  const db = await getDb();
  const key = `${fingerprintId}:${todayKey()}`;
  const last = db.data.sessionDedup[key];
  if (!last) return true;
  const hoursSince = (Date.now() - new Date(last).getTime()) / (1000 * 60 * 60);
  return hoursSince >= 4;
}

async function recordSession(fingerprintId) {
  const db = await getDb();
  const key = `${fingerprintId}:${todayKey()}`;
  db.data.sessionDedup[key] = new Date().toISOString();
  // Prune entries older than 2 days
  const cutoff = Date.now() - 2 * 24 * 60 * 60 * 1000;
  for (const k of Object.keys(db.data.sessionDedup)) {
    if (new Date(db.data.sessionDedup[k]).getTime() < cutoff) {
      delete db.data.sessionDedup[k];
    }
  }
  await db.write();
}

function todayKey() {
  return new Date().toISOString().slice(0, 10); // YYYY-MM-DD
}

module.exports = { linkFingerprint, getMemberByFingerprint, getAllMembers, canAwardSession, recordSession };
