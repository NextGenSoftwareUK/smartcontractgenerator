/**
 * OASIS API client
 *
 * Thin wrapper around https://api.oasisweb4.com
 * All Union features (quests, karma/tokens, holons, avatars) go through here.
 *
 * Docs:  https://api.oasisweb4.com/swagger/index.html
 * Auth:  Bearer JWT obtained via /api/avatar/authenticate
 */

const axios = require('axios');

const BASE = process.env.OASIS_API_BASE ?? 'https://api.oasisweb4.com/api';

// Cached JWT — the bridge authenticates as a service avatar
let _jwt = null;
let _jwtExpiry = 0;

async function getJWT() {
  if (_jwt && Date.now() < _jwtExpiry) return _jwt;

  const res = await axios.post(`${BASE}/avatar/authenticate`, {
    username: process.env.OASIS_SERVICE_USERNAME,
    password: process.env.OASIS_SERVICE_PASSWORD
  });

  if (res.data.isError) throw new Error(`OASIS auth failed: ${res.data.message}`);

  _jwt = res.data.result?.jwtToken ?? res.data.result?.token;
  // Tokens typically expire after 24 h; refresh after 20 h to be safe
  _jwtExpiry = Date.now() + 20 * 60 * 60 * 1000;
  return _jwt;
}

async function api(method, path, data) {
  const jwt = await getJWT();
  const res = await axios({
    method,
    url: `${BASE}${path}`,
    headers: { Authorization: `Bearer ${jwt}`, 'Content-Type': 'application/json' },
    data
  });
  if (res.data.isError) throw new Error(`OASIS error (${path}): ${res.data.message}`);
  return res.data.result;
}

// ─── Avatar ──────────────────────────────────────────────────────────────────

/**
 * Register a new OASIS avatar for a gym member.
 * Called once when a member is enrolled.
 */
async function registerAvatar({ username, email, password, firstName, lastName }) {
  const res = await axios.post(`${BASE}/avatar/register`, {
    username,
    email,
    password,
    confirmPassword: password,
    firstName,
    lastName,
    acceptTerms: true
  });
  if (res.data.isError) throw new Error(`Register failed: ${res.data.message}`);
  return res.data.result;
}

async function getAvatar(avatarId) {
  return api('get', `/avatar/get-by-id/${avatarId}`);
}

// ─── Karma (= Union Tokens) ───────────────────────────────────────────────────

/**
 * Award karma to an avatar.
 * Karma IS the Union token system — no separate currency needed.
 *
 * KarmaType options: HelpOthers, ShareKnowledge, BeInspired, BeInspiring,
 *   GivingGifts, RandomActs, etc.
 * We use custom types relevant to training context.
 */
async function addKarma({ avatarId, karmaType = 'BeInspiring', description = 'Training session' }) {
  return api('post', '/avatar/add-karma', {
    avatarId,
    KarmaType: karmaType,
    karmaSourceType: 'hApp',
    KaramSourceTitle: 'The Union PDC',
    KarmaSourceDesc: description
  });
}

async function getKarma(avatarId) {
  return api('get', `/avatar/get-karma/${avatarId}`);
}

// ─── Holons (= Session & Passport records) ────────────────────────────────────

/**
 * Save a holon. Pass id as null/omit for create; existing GUID for update.
 *
 * We store:
 *   - CheckInHolon  (one per gym visit)
 *   - PassportHolon (one per member, updated on each milestone)
 */
async function saveHolon(holon) {
  return api('post', '/data/save-holon', {
    holon: {
      id: holon.id ?? '00000000-0000-0000-0000-000000000000',
      name: holon.name,
      description: holon.description ?? '',
      holonType: holon.holonType ?? 40, // 40 = generic Holon
      parentHolonId: holon.parentHolonId ?? '00000000-0000-0000-0000-000000000000',
      metadata: holon.metadata ?? {},
      isActive: true
    },
    saveChildren: false
  });
}

async function getHolon(id) {
  return api('get', `/data/load-holon/${id}`);
}

async function searchHolons({ name, holonType } = {}) {
  return api('post', '/data/load-all-holons', {
    holonType: holonType ?? 'All',
    loadChildren: false
  });
}

// ─── Quests ───────────────────────────────────────────────────────────────────

/**
 * Create a quest in the STAR system.
 * Called by the seed script — not on every check-in.
 */
async function createQuest({ name, description }) {
  return api('post', '/quests', { name, description });
}

async function listQuests() {
  return api('get', '/quests');
}

async function getQuest(id) {
  return api('get', `/quests/${id}`);
}

/**
 * Mark a quest as complete for an avatar.
 */
async function completeQuest({ questId, avatarId }) {
  return api('post', '/quests/complete', { questId, avatarId });
}

// ─── NFTs (= Credentials & stamps) ───────────────────────────────────────────

/**
 * Mint a credential NFT to an avatar's wallet.
 * Used for passport stamps like "Visited Tulum", "100 Sessions", "Trainer Certified".
 */
async function mintCredential({ avatarId, name, description, imageUrl }) {
  return api('post', '/nft/create', {
    avatarId,
    name,
    description,
    imageUrl: imageUrl ?? '',
    metadata: { source: 'union-passport', type: 'credential' }
  });
}

module.exports = {
  registerAvatar,
  getAvatar,
  addKarma,
  getKarma,
  saveHolon,
  getHolon,
  searchHolons,
  createQuest,
  listQuests,
  getQuest,
  completeQuest,
  mintCredential
};
