# The Union — Chapters Model
## Franchise & Expansion Proposal

_Prepared for Mike & Jefferson · March 2026_

---

## The Idea in One Sentence

The Union PDC becomes the **Mother Chapter** of a global network of fight gyms — each location an official Chapter, united by shared branding, a digital passport system, a cross-gym quest network, and a shared merch store — with technology built on OASIS/STAR.

---

## Why "Chapters"?

Motorcycle clubs like the Hells Angels or the Mongols built a global brand from a single city through the **Chapters model**: one founding club, strict standards for new chapters, shared identity (colours, logo, name), local autonomy, and a loyalty system that means members of one chapter are welcomed at any other.

The Union is already that kind of gym. The name says it. We're formalising it.

---

## What a Chapter Gets

Every Official Chapter of The Union receives:

| Asset | Detail |
|---|---|
| **Brand licence** | Logo, IFC Los Banditos font, colour palette, brand guidelines |
| **Chapter page** on the Playa map | Pin on the live world map with location, schedule, and story |
| **Shared merch store** | Listed on `store.theunion.world` — sells Chapter-specific and shared gear |
| **Digital passport** | Members earn stamps in the OASIS passport for visiting, training, competing |
| **Quest network** | Challenges between Chapters — visit Tulum + PDC = unlock a reward |
| **Union token** | Internal points system — earned by training, competing, referring, buying |
| **Network marketing** | Listed in The Union directory, cross-promoted to all Chapter audiences |

---

## What a Chapter Owes

| Obligation | Amount / Terms |
|---|---|
| **Upfront licence fee** | TBD by Mike — covers branding kit + onboarding |
| **Revenue share on merch** | 5–15% of merch sold through the shared store |
| **Minimum training pedigree** | Franchisee must have trained at The Union PDC (sliding scale below) |
| **Brand standards** | Must use official assets, cannot modify logo/colours without approval |
| **Chapter membership in OASIS** | Gym registered as a Holon on STARNET |

### Sliding Scale — Training Pedigree

The longer you trained under Mike, the better your terms:

| Time Trained at PDC | Revenue Share | Upfront Fee |
|---|---|---|
| 6–12 months | 15% | High |
| 1–2 years | 10% | Medium |
| 2+ years | 5% | Low |
| Founded member / fighter | Negotiated | Negotiated |

This rewards loyalty and ensures every Chapter carries the real culture of the gym — not just the name.

---

## The Technology Stack

Everything below is **already built or in active development** on OASIS/STAR.

### 1. The Fighter Passport

Every member gets an OASIS Avatar — a digital identity that follows them across every Chapter.

Their passport records:
- Gyms trained at (with dates and session count)
- Seminars attended and coaches met
- Fights competed in
- Belts, ranks, and credentials earned
- Union tokens accumulated

When a member walks into The Union Tulum, the staff can pull up their passport and see they're a verified PDC member. When they visit The Union London, same thing. **No paperwork. No fake credentials.**

This is the same system used for verified professional credentials on OASIS — we're adapting it for the fight world.

```
AvatarHolon (The Fighter)
  └─ PassportHolon
       ├─ ChapterVisitHolon[]     (gym, date, session count)
       ├─ SeminarHolon[]          (coach, topic, chapter, date)
       ├─ CompetitionHolon[]      (event, result, weight class)
       ├─ CredentialHolon[]       (belts, certifications, rank)
       └─ UnionTokenBalance       (accumulated across all chapters)
```

### 2. The Chapter Map

Already live at [hh-pdc-wsyc.vercel.app/playa-map.html](https://hh-pdc-wsyc.vercel.app/playa-map.html) for Playa del Carmen.

As Chapters open, each one gets a pin on a **global version of this map** — dark, tactical, showing the full network. Tourists and fighters can see where their next training stop is. Locals can find their nearest Chapter.

Planned Chapter pins:
- 🐯 **The Union PDC** — Mother Chapter, Playa del Carmen
- 🐯 **The Union Tulum** — First affiliate (pending terms)
- 🐯 **The Union [City]** — Open

Each pin links to the Chapter's profile, schedule, passport stamp info, and active quests.

### 3. Cross-Chapter Quest System (Quest-to-Physical)

Built on the OASIS Q2P (Quest-to-Physical) system.

**Example quests:**
- Train at PDC + Tulum in the same month → earn the **"Riviera" stamp** + 20% off store
- Attend 3 seminars across any Chapter → unlock the **"Student of the Game"** credential
- Win a fight representing any Chapter → earn the **"Warrior" token drop**
- Refer a new member who trains for 3+ months → earn Union tokens

Quests are verified on the backend — no honour system, no paper forms. The OASIS API records completions and issues proofs. The store reads those proofs at checkout and applies discounts automatically.

```
QuestHolon
  ├─ Objective[]   (visit X chapter, complete Y sessions, attend Z seminar)
  ├─ ProofHolon    (signed completion record, stored in OASIS)
  └─ RewardHolon   (token drop / store discount / credential / NFT stamp)
```

### 4. Shared Merch Store

Already deployed at [github.com/The-Union-PDC/Store](https://github.com/The-Union-PDC/Store).

Built on Medusa v2 (headless commerce) + Next.js storefront. Each Chapter can list their own products alongside shared Union gear. Revenue from each Chapter's products flows to that Chapter; shared products split by agreed terms.

Quest proofs unlock discounts automatically at checkout — training is literally rewarded with cheaper gear.

**Domain target:** `store.theunion.world` (Namecheap + Vercel, free hosting)

### 5. The Union Token

An internal points currency, not a speculative crypto. Think frequent flyer miles for fighters.

Earned by:
- Training sessions logged
- Quests completed
- Fights won
- Merch purchased
- Members referred
- Reviews left

Spent on:
- Store discounts
- Priority class booking
- Seminar access
- Chapter merchandise drops
- Exclusive "Veteran" status unlocks

This is what makes franchising self-reinforcing — every Chapter feeds the same token economy. Joining the network means your members gain access to a bigger pool of rewards and recognition.

---

## The Chapters Rollout Plan

### Phase 0 — Mother Chapter (Now)
- [x] Online store deployed
- [ ] Migrate membership database to MongoDB
- [ ] Reserve all social handles
- [ ] Register Mike's gym as a Holon on STARNET
- [ ] Launch passport system for existing PDC members

### Phase 1 — First Chapter: Tulum
- [ ] Finalise franchise terms with Tulum prospect
- [ ] Add Tulum to the map
- [ ] Create first cross-chapter quest: PDC ↔ Tulum
- [ ] Issue shared branding kit
- [ ] Onboard Tulum members to passport system

### Phase 2 — Network Effects
- [ ] Open up Chapter applications (Jefferson's Colombia + UK contacts)
- [ ] Mike's SF coach's network — potential US Chapter
- [ ] Build "Chapter Directory" page on the website
- [ ] Launch Union token economy across all active Chapters

### Phase 3 — The Game
- [ ] Tourist-facing app: Pokemon Go meets Foursquare, set in real cities
- [ ] Players collect Union tigers by visiting Chapter locations
- [ ] Physical check-ins (QR at gym entrance) issue passport stamps
- [ ] 360 virtual gym environments via Blockade Labs + STAR API — train virtually between real visits

---

## Why This Works for Mike Right Now

Mike needs **cash flow, not a five-year plan**. Here's how this generates revenue fast:

| Revenue Stream | Timeline |
|---|---|
| Merch store (existing inventory) | Week 1 — store is live |
| Tulum franchise fee (upfront) | When terms agreed |
| Tulum revenue share on merch | Ongoing from day 1 |
| Token economy drives repeat visits | Month 1+ |
| Seminar income (Jefferson's network) | Month 1–3 |

The franchise fee from Tulum alone could be meaningful. The merch store requires zero new inventory — Mike already has stock. The passport system costs nothing to run once set up; it runs on OASIS infrastructure.

---

## The Pitch to Tulum

If the Tulum person wants to use The Union name, font, logo, and colours — they're already asking to be a Chapter. The question is just whether they pay for it properly.

The message is simple:

> *"You can open a gym called anything you want. But The Union is a brand that Mike built over decades. If you want to carry that name, you carry the standards that come with it — and you contribute back to the network that makes the name worth having."*

The passport system, the map, the quests, the store — these are the things that make the franchise fee worth paying. You're not just buying a logo. You're buying into a living network.

---

## Contact

- **Mother Chapter:** Mike, The Union PDC, Playa del Carmen
- **Tech & Operations:** Max Gershfield
- **Web & Brand:** Jefferson Sanchez — jeff.sanchez91@gmail.com
- **GitHub Org:** [github.com/The-Union-PDC](https://github.com/The-Union-PDC)
