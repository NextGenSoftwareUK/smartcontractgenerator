# Union API — Fingerprint Bridge to OASIS STAR

Connects The Union PDC's ZKTeco fingerprint reader to the OASIS STAR platform.

Every time a member scans their finger:
1. A **CheckIn holon** is saved to OASIS (permanent, verifiable record)
2. **Karma is awarded** to their OASIS avatar (karma = Union tokens)
3. **Quest milestones** are checked and completed in STAR
4. An optional **WhatsApp notification** is sent

No duplicate databases. No custom token system. Everything uses the OASIS APIs.

---

## Architecture

```
ZKTeco Reader
    │
    │ TCP poll (node-zklib) or HTTP PUSH
    ▼
union-api (Node.js / Express)
    │
    ├── lib/oasis.js        → OASIS REST API client
    ├── lib/checkin.js      → core session processing
    ├── lib/localDb.js      → fingerprintId → avatarId map (lowdb JSON)
    └── lib/notifier.js     → WhatsApp webhook stub
    │
    ▼
https://api.oasisweb4.com
    ├── /api/avatar         → member identity
    ├── /api/avatar/add-karma  → token rewards
    ├── /api/data/save-holon   → session records
    └── /api/quests/complete   → quest milestones
```

---

## Quick Start

```bash
cp .env.example .env
# Fill in OASIS_SERVICE_USERNAME and OASIS_SERVICE_PASSWORD

npm install
npm run seed-quests   # Creates Union quests in STAR, prints IDs → paste into .env
npm run dev           # Start server on :3001
```

---

## Enrolling a member

```bash
# 1. Register them — creates an OASIS avatar and links their fingerprint ID
curl -X POST http://localhost:3001/api/members/register \
  -H "Content-Type: application/json" \
  -d '{
    "fingerprintId": "42",
    "username": "carlos_pdm",
    "email": "carlos@example.com",
    "password": "SecurePass123!",
    "firstName": "Carlos",
    "lastName": "Pérez",
    "chapter": "PDC"
  }'

# 2. Carlos verifies his email (OASIS sends a link)

# 3. From now on, every fingerprint scan auto-awards karma and logs a session
```

---

## ZKTeco Configuration

### Option A — HTTP PUSH (newer devices, recommended)

In the device's web panel (`http://<device-ip>`):
- **ADMS → Server** settings
- Set server address to `http://<your-server>:3001`
- Set path to `/api/bridge/zkteco`
- Enable push

### Option B — TCP Poll (all devices)

Set in `.env`:
```
ZKTECO_BRIDGE_ENABLED=true
ZKTECO_DEVICE_IP=192.168.1.100
```

The server will poll the device every 5 minutes via port 4370.

---

## API Reference

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/members/register` | Create OASIS avatar + link fingerprint |
| POST | `/api/members/link` | Link fingerprintId to existing avatarId |
| GET | `/api/members` | List locally-known members |
| GET | `/api/members/:avatarId` | Get full OASIS avatar |
| POST | `/api/sessions/fingerprint` | Log session by fingerprintId |
| POST | `/api/sessions/manual` | Log session by avatarId (admin) |
| GET | `/api/sessions/:avatarId` | Get session holons from OASIS |
| GET | `/api/passport/:avatarId` | Full fighter passport from OASIS |
| POST | `/api/bridge/zkteco` | ZKTeco push receiver |

---

## OASIS Swagger

Full API docs: [https://api.oasisweb4.com/swagger/index.html](https://api.oasisweb4.com/swagger/index.html)
