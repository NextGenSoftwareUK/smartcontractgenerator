# Q2P Architecture

## Overview

Q2P combines:
1. **Vercel Commerce** – Storefront UI (product grid, cart, checkout)
2. **Medusa** – Product catalog, orders, inventory, Stripe
3. **OASIS** – Avatar identity, quest proofs, entitlement-based discounts

## Data Flow

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Next.js        │     │  Medusa         │     │  ONODE (OASIS)  │
│  Storefront     │────▶│  Backend        │     │  API            │
│  (Vercel        │     │  - Products     │     │  - Avatar       │
│   Commerce)     │     │  - Orders       │     │  - Quest proofs │
│                 │     │  - Stripe       │     │  - Entitlements │
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │
         │  Checkout: apply      │  Create order,        │  Get avatar,
         │  proof discount      │  charge Stripe        │  verify proof
         └──────────────────────┴───────────────────────┘
```

## Quest-to-Physical Flow

1. Player completes quest in OASIS (game, IsoCity, park, etc.)
2. ONODE creates **quest completion proof** (stored as holon)
3. Player visits Q2P shop, adds physical good to cart
4. At checkout: storefront calls `GET /api/quest/entitlements` for avatar
5. If proof matches product's required proof → discount applied
6. Stripe charges (discounted amount + shipping)
7. Order sent to fulfillment partner (HK)

## Medusa + OASIS

- **Medusa** handles: products, variants, cart, checkout, Stripe, fulfillment
- **OASIS** handles: who the user is (avatar), what they've earned (proofs)
- **Integration point:** Checkout applies discount from proof before Stripe charge
