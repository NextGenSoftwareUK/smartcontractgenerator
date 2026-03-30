# Q2P Template & Linking Store to Portal Quests – Summary

This doc summarises your **initial research** on (1) turning Q2P into a **template** and (2) **linking physical prizes to completing quests** inside your portal, and what’s in place vs what’s left to build.

---

## 1. Vision (from your research)

| Idea | Description |
|------|-------------|
| **Quest-to-Physical** | Players earn real-world merchandise (discounts, exclusives) by completing in-world quests. |
| **Core loop** | “Find it, solve it, or master it inside OASIS → earn it in the real world.” |
| **Template** | Q2P as a **reusable OAPP template** so builders can spin up new “quest‑to‑physical” shops (STAR CLI: `star create oapp "My Shop" --template q2p-shop`). |
| **Portal link** | Quests completed **in the portal** (games, IsoCity, worlds) issue **verified proofs**; the **store** reads those proofs and applies discounts / unlocks at checkout. |

So: **Portal (quests) → OASIS (proofs) → Store (discounts / physical prizes).**

---

## 2. What you already have

### 2.1 Store (Q2P) – Medusa + Next.js

- **Medusa** – Products, cart, orders, Stripe.
- **Storefront** – Next.js (Vercel Commerce); product grid, cart, checkout.
- **OASIS client stub** – `storefront/lib/oasis.ts`: `getEntitlements(token)`, `getDiscountForProof(proof, skuId)`. Currently calls `GET /api/quest/entitlements` (not implemented on ONODE yet), returns `[]` on failure.

So the **store is ready to consume** entitlements once the backend exposes them.

### 2.2 Template (OAPP)

- **`Q2P/oapp-template/`** – OAPP template source.
- **`OAPPTemplateDNA.json`** – Name “Q2P Shop”, `templateType: "q2p-shop"`, features include `quest-proof-discounts`.
- **STAR CLI** – Publish template, then `star create oapp "My Q2P Shop" --template q2p-shop` to create new shops from the template.

So the **template exists**; it becomes a “real” template once STAR publish flow is used and the store is stable.

### 2.3 Portal & quests (ONODE / STAR)

- **Quest completion** – `POST /api/quest/complete` (gameId + objectiveKey), `POST /api/world/{worldId}/complete-quest` (e.g. IsoCity). Today: XP, karma, some NFT minting.
- **Quest Registry** – **Documented** (e.g. `QUEST_REGISTRY_SDK_AND_QUEST_SYSTEM.md`, `QUEST_TO_PHYSICAL_Q2P_BUILD_PLAN.md`) but **not implemented**: no single registry of objectives with rewards (including physical).
- **Proofs / entitlements** – **Not implemented**: no proof holon, no `GET /api/quest/entitlements`, no redemption API.

So the **portal can complete quests**, but there is **no registry-driven physical reward** and **no proof/entitlement API** for the store to use.

---

## 3. Gaps (to link store ↔ portal quests)

From **QUEST_TO_PHYSICAL_Q2P_BUILD_PLAN.md** and **QUEST_REGISTRY_SDK_AND_QUEST_SYSTEM.md**:

| Gap | What’s needed |
|-----|----------------|
| **Quest Registry** | DB/config in ONODE: `gameId`, `objectiveKey`, name, description, karma, optional **physicalReward** (e.g. discount %, tier, skuIds). |
| **Physical reward on completion** | When a registered objective has `physicalReward`, completion creates a **proof** (see below). |
| **Proof storage** | “Quest completion proof” holon (or equivalent): avatarId, questId/gameId+objectiveKey, completedAt, tier, physicalReward, redeemed. |
| **Entitlement API** | `GET /api/quest/entitlements` (for current avatar) and optionally `GET /api/quest/entitlements/{proofId}` so the **store** can fetch proofs and apply discounts. |
| **Checkout integration** | Storefront: at checkout, call entitlements (with avatar token), match cart items to proof tier/SKU, apply discount before Stripe. |
| **Redemption (later)** | `POST /api/quest/redeem` (proofId + skuId), mark proof used, optionally trigger fulfillment. |

Until the **registry + proof + entitlement API** exist, the store has **nothing to call** beyond the stub in `lib/oasis.ts`.

---

## 4. How the link would work (end-to-end)

1. **Portal** – User signs in (avatar + JWT). Plays a game/world; game calls e.g. `completeObjective('isocity', 'population_100')` (or world completion endpoint).
2. **ONODE** – Looks up (`gameId`, `objectiveKey`) in **Quest Registry**. If it has a **physicalReward**, creates a **QuestCompletionProof** (avatarId, tier, discount%, skuIds, etc.) and stores it.
3. **Store** – User visits Q2P shop (same avatar/token). At checkout, storefront calls `GET /api/quest/entitlements` with the avatar token.
4. **ONODE** – Returns list of proofs for that avatar (e.g. `{ proofId, tier, physicalReward: { type: 'discount', value: 10, skuIds: ['MERCH-001'] } }`).
5. **Store** – For each cart line, `getDiscountForProof(proof, skuId)` (or equivalent); apply discount, then charge Stripe (discounted amount + shipping).
6. **(Later)** – Redemption API marks proof as used and can trigger fulfillment partner.

So: **Portal quest completion → ONODE registry + proof → Store reads entitlements → discount at checkout.**

---

## 5. Phased build (from your plan)

- **Phase 1** – Quest Registry (with `physicalReward`), wire completion API to registry, create proof on completion when physical reward exists.
- **Phase 2** – Entitlement API (`GET /api/quest/entitlements`), proof storage (holon or equivalent).
- **Phase 3** – Redemption API, fulfillment webhook, platform fees.
- **Phase 4** – Q2P OAPP template polish, questline ↔ registry, docs for builders.

Template “works” as soon as the store runs and can be published; **linking to quests** needs at least **Phase 1 + Phase 2** so the store has an entitlement API to call.

---

## 6. References in this repo

| Doc / code | Purpose |
|------------|--------|
| **Docs/QUEST_TO_PHYSICAL_Q2P_BUILD_PLAN.md** | Full Q2P build plan, registry, proof, entitlement, redemption, API list, data models. |
| **Docs/QUEST_REGISTRY_SDK_AND_QUEST_SYSTEM.md** | Quest registry, OASIS Game SDK, who creates quests, portal ↔ games flow. |
| **Q2P/docs/ARCHITECTURE.md** | Q2P data flow: storefront ↔ Medusa ↔ OASIS (avatar, proofs, entitlements). |
| **Q2P/README.md** | Stack, env vars, planned Q2P integration (proofs, entitlements API). |
| **Q2P/oapp-template/README.md** | How to publish and create shops from the Q2P template. |
| **Q2P/storefront/lib/oasis.ts** | Client for entitlements and discount-from-proof; ready once API exists. |

---

## 7. Suggested next steps

1. **Stabilise the store** – Get the current Q2P storefront and Medusa deploy working (you’re close; we’ve been fixing Server Component/errors).
2. **Implement Quest Registry + proof (Phase 1)** – In ONODE: registry storage, extend `POST /api/quest/complete` (and world complete) to use registry and create a proof when `physicalReward` is set.
3. **Implement Entitlement API (Phase 2)** – `GET /api/quest/entitlements` (and optionally by proofId), returning proofs for the authenticated avatar so the store can apply discounts.
4. **Wire storefront to entitlements** – At checkout, pass avatar token, call `getEntitlements(token)`, apply `getDiscountForProof(proof, skuId)` per line item, then Stripe.
5. **Treat Q2P as the reference template** – Once the above works, document it and use the existing oapp-template + STAR CLI so “linking the store to quests” is the default behaviour of the template.

If you tell me whether you want to move first on **ONODE (registry + proof + entitlements)** or **storefront (checkout + discount UI)**, I can outline concrete tasks and file/endpoint names next.
