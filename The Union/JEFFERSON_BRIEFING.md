# The Union PDC — Tech Briefing for Jefferson
*Updated: March 2026 · Prepared by Max*

---

## What we've built so far

Here's a quick summary of what's already done and sitting in the GitHub org:

### 1. Online Store — `store.theunion...`
A fully branded Union store is ready to deploy. It has:
- The Union dark gold branding, logo, fonts
- Product pages (Mike connects his merch inventory via the Medusa admin panel)
- A working checkout (address, shipping, payment via Stripe)
- A footer with a "Find us on the map" link to the Playa map

**What it needs from you:** A domain pointed at Vercel (Namecheap → Vercel, free). The repo is at [github.com/The-Union-PDC/Store](https://github.com/The-Union-PDC/Store).

---

### 2. Fighter Passport & Token System
When a member checks in at the gym, the system:
1. Logs the session as a permanent, verifiable record (on the OASIS platform)
2. Awards them "karma" — our internal points currency (no crypto, just loyalty points)
3. Checks whether they've hit a milestone (10 sessions, 50 sessions, etc.)
4. Sends them a **WhatsApp message** — instantly, on the phone they already have

Members never need to download anything. They get a WhatsApp message every time they train.

Example messages:
> *"Welcome back, Carlos! ✅ Session logged at The Union PDC.*
> *+10 karma 🐯 — your total is 240. Reply BALANCE to check your progress."*

> *"🏆 Quest unlocked: 50 Sessions — Fighter Status! You've earned 150 bonus karma.*
> *You're now a Fighter. Reply PASSPORT to see your full record."*

Members can reply to any message:
- **BALANCE** → shows current karma and what it unlocks
- **PASSPORT** → full summary (tier, sessions, quests, chapter stamps)
- **QUESTS** → what they're working toward
- **DISCOUNT** → generates a store discount code if they have enough karma

---

### 3. The Fingerprint Connection
The existing fingerprint reader at the gym already captures every check-in. We've built a bridge that connects it to the system above — no changes to the device needed, we just read what it already records.

---

## What I need from you, Jefferson

### Priority 1 — WhatsApp (1 hour, this week)

We're using Meta's own WhatsApp API — **completely free**, no third-party subscription, no monthly fee.

**Steps:**

1. **Create a Meta Developer account** (if you don't have one)
   → Go to [developers.facebook.com](https://developers.facebook.com) and sign in with your Facebook account

2. **Create a new app**
   - Click "Create App"
   - Choose "Business" as the app type
   - Give it a name: `The Union PDC`

3. **Add WhatsApp to the app**
   - In the app dashboard, find "Add a Product" → click "Set up" on WhatsApp
   - Follow the setup steps — you'll link it to a Meta Business Account

4. **Get your credentials** (two values we need)
   - In the WhatsApp section, go to **API Setup**
   - Copy the **Phone Number ID** — looks like: `123456789012345`
   - Copy the **Temporary Access Token** (or generate a permanent one)
   - Send these to Max securely (WhatsApp or Signal)

5. **Phone number decision**
   Two options:
   - **Option A:** Use Mike's existing gym WhatsApp number. This requires porting the number into Meta Business (Meta walks you through it). Members already have this number saved.
   - **Option B:** Start with the free test number Meta provides while we're building. Switch to Mike's number when ready.

   **Recommendation:** Start with the test number, switch when everything is working.

6. **Set the webhook URL** (Max will give you this once the server is deployed)
   - In the WhatsApp API Setup page, find "Webhook"
   - Set the URL to: `https://union-api.railway.app/api/bridge/whatsapp`
   - Set the verify token to: `union_webhook_verify` (or whatever Max confirms)
   - Subscribe to: `messages`

---

### Priority 2 — Deploy the API server (30 mins, this week)

The `union-api` backend needs to run somewhere. We're using **Railway** — free tier, no credit card needed to start.

**Steps:**

1. Go to [railway.app](https://railway.app) and sign in with GitHub
2. Click "New Project" → "Deploy from GitHub repo"
3. Select `The-Union-PDC/Store`
4. Set the root directory to `union-api`
5. Add environment variables (Max will send you the list)
6. Railway gives you a public URL — send it to Max

---

### Priority 3 — Migrate the existing website (when you have time)

You mentioned the existing Union website code is somewhere. Can you move it into the GitHub org?

1. Go to [github.com/The-Union-PDC](https://github.com/The-Union-PDC)
2. Create a new repo called `Website`
3. Push the existing site code there

This keeps everything in one place and makes it easier to deploy.

---

### Priority 4 — Social handle protection (urgent)

As discussed — someone is trying to use The Union name in Tulum. Please reserve these handles as soon as possible:

- [ ] Instagram: `@theunionpdc` (or `@theunion.pdc`)
- [ ] Facebook page: `The Union PDC`
- [ ] TikTok: `@theunionpdc`
- [ ] YouTube: `The Union PDC`
- [ ] X/Twitter: `@theunionpdc`

---

### Priority 5 — Member list (when ready)

When the system is live, we'll need to enrol members one by one. For each member we need:
- Name
- WhatsApp number (with country code, e.g. `+52 984 123 4567`)
- Their fingerprint ID from the device (the number the reader assigns them)

Jefferson — can you find out from Mike how to export the member list from the old software? Even a screenshot of the member list would be a start.

---

## What happens once you've done the above

Once the WhatsApp credentials are in and the server is live:

1. **Enrol a test member** (you or Max) — takes 30 seconds via a simple form
2. **Scan the fingerprint reader** — within seconds, a WhatsApp message arrives
3. **Reply BALANCE** — see the karma total
4. **Show Mike** — that's the demo

From there we add members one by one. The fingerprint reader handles everything automatically — no ongoing work for you or Mike.

---

## Links

- GitHub org: [github.com/The-Union-PDC](https://github.com/The-Union-PDC)
- Store repo: [github.com/The-Union-PDC/Store](https://github.com/The-Union-PDC/Store)
- Playa map: [hh-pdc-wsyc.vercel.app/playa-map.html](https://hh-pdc-wsyc.vercel.app/playa-map.html)
- Meta developer docs: [developers.facebook.com/docs/whatsapp/cloud-api](https://developers.facebook.com/docs/whatsapp/cloud-api)
- Railway: [railway.app](https://railway.app)

---

*Any questions — WhatsApp Max directly. This doc is in the GitHub repo at `docs/JEFFERSON_BRIEFING.md`.*
