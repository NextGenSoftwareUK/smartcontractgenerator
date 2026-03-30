# Plan: Link Q2P Store to Quest System

This document is the **execution plan** to connect the Q2P storefront to the existing ONODE quest/entitlement system so that quest completion proofs unlock discounts at checkout.

---

## Current State

| Layer | Status |
|-------|--------|
| **ONODE** | Quest registry (config), QuestProofService (create/get proofs), QuestController.Complete + GetEntitlements exist. Entitlements return `QuestCompletionProof` with `PhysicalRewardJson` (string). |
| **Storefront** | `lib/oasis.ts`: `getEntitlements(token)`, `getDiscountForProof(proof, skuId)`. Expects `physicalReward` object; not wired into cart or checkout. |
| **Auth** | GET /api/quest/entitlements requires `Authorization: Bearer <jwt>`. Storefront has no OASIS token flow yet (can use env/cookie for MVP). |

---

## Plan (Tasks)

### 1. ONODE: Entitlements API response shape

- **Goal:** Storefront expects `physicalReward: { type, value, skuIds }`. ONODE currently returns `PhysicalRewardJson` (string).
- **Action:** Add a DTO or serialize `QuestCompletionProof` with a parsed `PhysicalReward` property so the JSON response includes `physicalReward` as an object. Keep `PhysicalRewardJson` for backward compatibility if needed.
- **Files:** `QuestProofService.cs` (or controller) – map to response DTO that parses JSON into `PhysicalReward`.

### 2. Storefront: Robust entitlements client

- **Goal:** Handle ONODE response (e.g. `{ result: [...] }`), support both `physicalReward` and `physicalRewardJson` (parse if string).
- **Action:** In `lib/oasis.ts`: normalize API response to `QuestProof[]`; if `physicalReward` is missing but `physicalRewardJson` exists, parse JSON into `physicalReward`.
- **Token:** Add a way to get the OASIS JWT (e.g. `getOasisToken()` reading from cookie `oasis_token` or env `NEXT_PUBLIC_OASIS_AVATAR_TOKEN` for dev). If no token, `getEntitlements` returns `[]`.

### 3. Storefront: Cart modal – entitlements + discount UI

- **Goal:** When the cart has items, show “Quest discount” and a discounted total when the user has applicable proofs.
- **Action:**
  - In the cart modal (client component), if OASIS token is available, call `getEntitlements(token)`.
  - For each cart line, compute discount: use `merchandise.id` (variant id) and `variant?.sku` with `getDiscountForProof(proof, skuId)` (try both variant id and sku; take max discount per line).
  - Sum line-level discounts; show a “Quest discount” row and “Total after discount” (subtotal − quest discount; tax/shipping still “at checkout”).
- **Files:** `components/cart/modal.tsx`, optionally a small `lib/oasis.ts` helper for “compute cart quest discount”.

### 4. Storefront: Checkout page – show quest discount

- **Goal:** Checkout page (currently static content) shows that a quest discount applies when the user has entitlements.
- **Action:** On the checkout page, fetch entitlements (if token available); if any proof applies to the cart (e.g. generic discount), show a short line: “You have a quest reward discount; it will be applied at payment.” Link to cart to see breakdown. (Full Medusa checkout flow can apply the discount in a later phase.)

### 5. (Optional) Medusa discount application

- **Goal:** Actually reduce the amount charged (e.g. via Medusa discount code or custom discount).
- **Action:** Defer to a later phase. For MVP, display-only discount in cart/checkout is sufficient.

---

## Execution Order

1. **ONODE** – Entitlements response includes `physicalReward` object.
2. **Storefront** – `lib/oasis.ts`: parse response + `physicalRewardJson`, add `getOasisToken()`.
3. **Storefront** – Cart modal: fetch entitlements, compute discount, show “Quest discount” and discounted total.
4. **Storefront** – Checkout page: fetch entitlements, show “quest discount will be applied” when applicable.
5. **Doc** – Update Q2P_TEMPLATE_AND_QUEST_LINK_SUMMARY or ARCHITECTURE to “linked” once the above is done.

---

## Success Criteria

- Avatar with a quest proof (with physical reward) sees a quest discount in the cart when the cart has matching (or any) items.
- Cart modal shows “Quest discount: -X%” and a lower total.
- Checkout page indicates that a quest discount will be applied when the user has entitlements.
- Without a token or without proofs, cart and checkout behave as today (no discount row).

---

## Execution Summary (Done)

| Task | Status | Notes |
|------|--------|------|
| 1. ONODE entitlements response | Done | `QuestEntitlementDto` + `PhysicalRewardDto` in `Models/Quest/QuestEntitlementDto.cs`; `QuestController.GetEntitlements` returns DTOs with parsed `PhysicalReward`. |
| 2. Storefront oasis client | Done | `lib/oasis.ts`: `getOasisToken()`, response normalizer (camel/Pascal + `physicalRewardJson`), `computeCartQuestDiscount`, `getQuestDiscountPercentForLine`. |
| 3. Cart modal | Done | Fetches entitlements when token + cart; shows "Quest discount" row and discounted total. |
| 4. Checkout page | Done | `components/checkout/checkout-quest-banner.tsx` shows when user has unredeemed proofs. |
| 5. Env example | Done | `.env.example` documents `NEXT_PUBLIC_OASIS_AVATAR_TOKEN`. |

---

## References

- ONODE: `QuestController.cs`, `QuestProofService.cs`, `QuestRegistryService.cs`
- Storefront: `lib/oasis.ts`, `components/cart/modal.tsx`, `app/[page]/page.tsx` (checkout)
- Docs: `QUEST_TO_PHYSICAL_Q2P_BUILD_PLAN.md`, `Q2P_TEMPLATE_AND_QUEST_LINK_SUMMARY.md`, `ARCHITECTURE.md`
