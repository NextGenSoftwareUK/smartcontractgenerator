# Q2P – Quest-to-Physical Commerce

Etsy-style virtual shop platform for OASIS. Players earn real-world merchandise discounts and access by completing in-world quests.

**Stack:**
- **Frontend:** Vercel Commerce (Next.js 13, App Router) – from [medusajs/vercel-commerce](https://github.com/medusajs/vercel-commerce)
- **Backend:** Medusa v2 (headless commerce)
- **Payments:** Stripe
- **Identity & Rewards:** OASIS (avatar, quest proofs, entitlements)

## Prerequisites

- Node.js 20+
- PostgreSQL (running locally or URL)
- Redis (optional, for caching)
- pnpm

## Quick Start

### 1. Prerequisites

- **PostgreSQL** – Medusa v2 requires it. Install via [Postgres.app](https://postgresapp.com/) (Mac), Homebrew (`brew install postgresql`), or Docker.
- **Node.js 20+** and **pnpm** (or npm)

### 2. Database

```bash
# Create the database (adjust user/password if needed)
createdb medusa-q2p
# Or with Docker: docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=postgres postgres
```

### 3. Medusa backend

```bash
cd Q2P/medusa
pnpm install
# Edit .env if DATABASE_URL differs (default: postgres://postgres:postgres@localhost:5432/medusa-q2p)

# Run migrations and start
pnpm run dev
# In another terminal, seed products (T-Shirt, Sweatshirt, Sweatpants, Shorts):
pnpm run seed
```

Medusa API: http://localhost:9000

### 4. Storefront

```bash
cd Q2P/storefront
pnpm install   # or npm install
pnpm dev       # or npm run dev
```

Storefront: http://localhost:3000

### 5. Connect storefront to Medusa

After seeding, get the publishable API key from Medusa (it’s created by the seed). Add to `storefront/.env.local`:

```
MEDUSA_API_KEY=<publishable_key_from_medusa>
```

Or call Medusa admin API to list keys. The seed creates a "Webshop" publishable key.

## Project Structure

```
Q2P/
├── medusa/          # Medusa v2 backend (products, orders, Stripe)
├── storefront/      # Next.js Commerce storefront (Vercel Commerce)
├── docs/            # Architecture, Q2P integration
├── oapp-template/   # STAR ODK OAPP template for spinning up shops
└── package.json     # Root scripts
```

## Environment Variables

| Variable | Location | Purpose |
|----------|----------|---------|
| `DATABASE_URL` | medusa/.env | PostgreSQL connection |
| `STRIPE_API_KEY` | medusa/.env | Stripe secret key |
| `STRIPE_WEBHOOK_SECRET` | medusa/.env | Stripe webhooks (production) |
| `NEXT_PUBLIC_MEDUSA_BACKEND_API` | storefront/.env.local | Medusa API URL (default: http://localhost:9000) |
| `MEDUSA_API_KEY` | storefront/.env.local | Publishable API key from Medusa admin |
| `NEXT_PUBLIC_OASIS_ONODE_URL` | storefront/.env.local | OASIS API for avatar & quest proofs |

## Q2P Integration (Planned)

- **Quest proofs** unlock discounts at checkout
- **Avatar** from OASIS used for identity
- **Entitlements API** (`/api/quest/entitlements`) provides proof-based discounts

See [Docs/QUEST_TO_PHYSICAL_Q2P_BUILD_PLAN.md](../Docs/QUEST_TO_PHYSICAL_Q2P_BUILD_PLAN.md) for the full integration plan.

## OAPP Template

Use STAR CLI to create new shops from the Q2P template:

```bash
cd "STAR ODK/NextGenSoftware.OASIS.STAR.CLI"
star create oapp "My Q2P Shop" --template q2p-shop
# Or: oapp create (Light Wizard)
```
