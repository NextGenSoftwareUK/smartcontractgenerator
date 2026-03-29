# Holonic SOP System — Full Documentation

**Version:** 1.0  
**Branch:** `max-build4`  
**Last updated:** March 2026  

---

## Table of Contents

1. [What Is This?](#1-what-is-this)
2. [The Problem It Solves](#2-the-problem-it-solves)
3. [Why This Is Different](#3-why-this-is-different)
4. [Why Now](#4-why-now)
5. [System Architecture](#5-system-architecture)
6. [Component Deep-Dive](#6-component-deep-dive)
   - [SOP App (Frontend)](#61-sop-app-frontend)
   - [STAR Watch (CLI Daemon)](#62-star-watch-cli-daemon)
   - [STAR API (Backend)](#63-star-api-backend)
   - [STARNET (On-Chain Proof Layer)](#64-starnet-on-chain-proof-layer)
7. [End-to-End Process Flow](#7-end-to-end-process-flow)
8. [Integrations](#8-integrations)
9. [The Proof Holon — Immutable Audit Trail](#9-the-proof-holon--immutable-audit-trail)
10. [API Reference](#10-api-reference)
11. [Running the System Locally](#11-running-the-system-locally)
12. [Demo Scripts](#12-demo-scripts)
13. [Roadmap](#13-roadmap)
14. [Commercial Opportunity](#14-commercial-opportunity)

---

## 1. What Is This?

The **Holonic SOP System** is a living, intelligent, auditable Standard Operating Procedure platform built on OASIS STAR.

A Standard Operating Procedure (SOP) is a documented set of steps that a team follows to complete a recurring process — onboarding a new customer, closing a deal, running a compliance check, processing a shipment. Every company has them. Most are written in Word documents, PDFs, Notion pages, or lived only in people's heads.

This system transforms those static documents into **executable, AI-native, blockchain-verified operating procedures** that:

- **Know when they are being followed** — by watching the tools teams already use (Slack, Salesforce, email)
- **Guide teams through each step** — surfacing the right instruction at the right moment, in the tools they're already in
- **Verify completion automatically** — matching real-world actions to SOP steps with AI confidence scoring
- **Request human sign-offs** — escalating to the right person via Slack when a step requires authorisation
- **Write an immutable proof** — every completed step is recorded on STARNET, creating a tamper-proof audit trail
- **Evolve over time** — learning from how teams actually work to improve the SOP itself

This is not a task manager, a workflow tool, or an AI assistant. It is an operating system for how organisations get things done — one that sits invisibly across every tool a company uses and surfaces intelligence without asking teams to change how they work.

---

## 2. The Problem It Solves

### The SOP Gap

Every organisation above a certain size has SOPs. Almost none of them work the way they were designed to.

**The reasons are consistent:**

**1. SOPs live in documents, work happens in tools.**  
A salesperson closes a deal in Salesforce. The onboarding SOP lives in Notion. Nobody opens Notion. The SOP is invisible at the moment of action.

**2. Compliance is manual and retrospective.**  
Audits ask "did you follow the process?" The only answer available is "I think so." There is no verifiable record of what happened, in what order, by whom, and when.

**3. SOPs go stale immediately.**  
The moment a SOP is written, the process starts drifting. New tools, new team members, new regulations. Nobody updates the document. The written SOP and the real process diverge silently.

**4. Institutional knowledge walks out the door.**  
When an experienced team member leaves, the undocumented version of "how we actually do this" leaves with them. What remains is a document that no longer reflects reality.

**5. AI can generate SOPs but cannot verify them.**  
AI tools can write a SOP from a prompt. They cannot tell you whether the team followed it, who deviated, why, or what the consequence was. Generation without verification is just more documentation noise.

### The Scale of the Problem

- The global Business Process Management (BPM) market is $14.4 billion (2024), projected to reach $26.1 billion by 2030
- Compliance failures cost enterprises an average of $14.8 million per incident (IBM, 2023)
- Knowledge workers spend an estimated 20% of their time searching for information about processes and procedures
- 68% of organisations report that their SOPs are "out of date" (Gartner, 2023)

---

## 3. Why This Is Different

Most SOP and compliance tools solve one piece of the problem:

| Tool Category | What They Do | What They Miss |
|--------------|--------------|----------------|
| Document tools (Notion, Confluence) | Store SOPs | No execution, no verification |
| Workflow tools (Monday, Asana) | Track task completion | Not connected to real work, requires manual updates |
| BPM platforms (Salesforce Flows, Zapier) | Automate workflows | Rigid, requires technical setup, no AI understanding |
| AI assistants (ChatGPT, Copilot) | Generate SOP content | No verification, no identity, no audit trail |
| Compliance tools (Vanta, Drata) | Evidence collection | Post-hoc, manual, not real-time |

**The Holonic SOP System sits in a different category.** It combines:

- **Real-time observation** — watching tools, not asking people to report back
- **Semantic understanding** — knowing that "sent the intro email" means the "welcome communication" step is done
- **Identity** — linking actions to verified OASIS Avatars (persistent, cross-platform digital identity)
- **Immutability** — writing proof to STARNET so the record cannot be altered
- **Evolution** — improving the SOP based on how the process actually runs

No existing tool does all five. The combination is the breakthrough.

---

## 4. Why Now

Three things have converged to make this possible and necessary:

**1. AI can understand intent, not just keywords.**  
Until recently, matching "I've sent the client the proposal" to a SOP step called "Send commercial proposal" required manual tagging, rigid keyword rules, or human review. BRAID (Behavioural Reasoning and AI Decision Engine) can do this semantically, with a confidence score, in real time.

**2. Distributed work has broken traditional oversight.**  
Post-2020, teams are remote, async, spread across time zones and tools. The informal "I can see you doing it" oversight of a physical office no longer exists. Companies desperately need a way to know that processes are being followed without micromanagement.

**3. Regulation is catching up to digital work.**  
EUDR (supply chain), DORA (financial services), SOC2, ISO 27001, ESG reporting — regulators are now requiring companies to prove, not just assert, that they followed the right processes. An immutable, timestamped, identity-linked audit trail is no longer a nice-to-have.

---

## 5. System Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        SOP APP (React/Vite)                          │
│   Authoring  │  Runner  │  Intel  │  Connections  │  Live Feed      │
└─────────────────────────┬───────────────────────────────────────────┘
                          │ REST (localhost:5001)
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    STAR API (.NET / ASP.NET Core)                    │
│                                                                      │
│  /api/avatar/authenticate    — JWT auth                              │
│  /api/workflow/save          — persist SOP as Holon                  │
│  /api/workflow/execute       — run a workflow                        │
│  /api/workflow/runs          — list active runs          [NEW]       │
│  /api/workflow/runs/start    — start a new run           [NEW]       │
│  /api/workflow/execution/:id — poll run state            [NEW]       │
│  /api/workflow/execution/:id/steps/:stepId/complete      [NEW]       │
│  /api/workflow/runs/:id/steps/:stepId/signoff            [NEW]       │
│  /api/braid/match            — semantic event matching   [NEW]       │
│  /api/Holons/create          — write proof holon                     │
└──────────────┬──────────────────────────────────────────────────────┘
               │
    ┌──────────┴──────────┐
    │                     │
    ▼                     ▼
┌──────────┐      ┌──────────────┐
│ MongoDB  │      │   STARNET    │
│ (Avatar  │      │ (Blockchain  │
│  State)  │      │  Proof Layer)│
└──────────┘      └──────────────┘

─────────────────────────────────────────────────────────────────────

┌─────────────────────────────────────────────────────────────────────┐
│                    STAR WATCH (Node.js / TypeScript CLI)             │
│                                                                      │
│  Watchers          Matcher           Action Engine                   │
│  ┌──────────┐      ┌──────────┐      ┌──────────────────────────┐  │
│  │  Slack   │ ───► │  BRAID   │ ───► │  auto_complete            │  │
│  │ (Socket  │      │  match   │      │  escalate (Slack card)    │  │
│  │  Mode)   │      │  +keyword│      │  soft_notify              │  │
│  └──────────┘      │  fallback│      │  log_only                 │  │
│  ┌──────────┐      └──────────┘      └──────────────────────────┘  │
│  │Salesforce│                                                        │
│  │ (Polling │        ▼                          ▼                   │
│  │   8s)    │  ┌──────────┐             ┌──────────────┐           │
│  └──────────┘  │ Run Cache│             │ Proof Holon  │           │
│                │(30s TTL) │             │ → STARNET    │           │
│  Local API     └──────────┘             └──────────────┘           │
│  :3001 ────────────────────────────────► SOP App Live Feed          │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 6. Component Deep-Dive

### 6.1 SOP App (Frontend)

**Location:** `SOP/sop-app/`  
**Stack:** React 18, TypeScript, Vite, `@phosphor-icons/react`, Space Grotesk + Instrument Serif fonts  
**Running:** `npm run dev` → `http://localhost:5176`

The SOP app is the primary interface for creating, running, and monitoring SOPs. It is designed around a dark, information-dense aesthetic inspired by Cursor and port-dashboard — precise, professional, and built for operators not consumers.

#### Pages

**`/` — Home**  
Dashboard overview: active runs, saved workflows, stat cards, and the **STAR Watch Live Feed** panel showing real-time events from all connected tools. Polls `http://localhost:3001/events` every 5 seconds. Shows two columns: raw detected events (source, action, entity, time) and matched SOP steps (step name, SOP name, confidence, action taken).

**`/authoring` — AI SOP Authoring**  
Describe a process in plain English. The system calls BRAID to generate a structured SOP with named steps, trigger conditions, sign-off requirements, and evidence fields. The generated SOP can be edited inline, saved to STARNET, and immediately launched in the Runner.

**`/runner/:workflowId` — SOP Runner**  
Step-by-step guided execution of a SOP. Shows the current step, what action is required, a notes field, evidence upload, and a sign-off toggle. Calls the STAR API to start a run, polls every 3 seconds for live status updates (steps completed by STAR Watch appear automatically), and marks steps complete. Produces a proof holon on completion.

**`/intel` — SOP Intelligence**  
Analytics and AI recommendations. Shows run history, completion rates, deviation patterns, and step-level timing. Will surface BRAID-generated improvement suggestions as more run data accumulates.

**`/connections` — Connector Management**  
Admin panel for STAR Watch integrations. Shows each connector (Slack, Salesforce, HubSpot, etc.) with its connection status, event count, and setup command. Setup panels walk through the `star connect` CLI commands. Uses the Brandfetch CDN for connector logos.

#### Key Components

```
src/
├── api/
│   └── client.ts          — STAR API wrapper (auth, workflows, executions)
├── components/
│   ├── Badge.tsx           — Status badges with pangea-style inset shadow
│   └── Card.tsx            — Panel container + StatCard
├── pages/
│   ├── Home.tsx            — Dashboard + live feed
│   ├── SOPAuthoring.tsx    — AI authoring + save/run
│   ├── SOPRunner.tsx       — Step-by-step runner
│   ├── SOPIntel.tsx        — Analytics
│   └── Connections.tsx     — Connector management
└── App.tsx                 — Sidebar nav + routing
```

#### Design System

- **Colours:** Dark charcoal (`#111`) base, `#2DD4BF` teal accent, `#C4C4C4` primary text
- **Typography:** `Instrument Serif` for display headings, `Space Grotesk` for UI, `JetBrains Mono` for code/IDs
- **Borders:** 0.5px `rgba(255,255,255,0.08)` with hairline top gradient on panel cards
- **Buttons:** Pangea-inspired inset box-shadow for depth, `translateY(1px)` on active
- **Icons:** `@phosphor-icons/react` throughout — no emoji, no lucide
- **Animations:** `fadeUp` entrance animations staggered across sections

---

### 6.2 STAR Watch (CLI Daemon)

**Location:** `SOP/star-watch/`  
**Stack:** Node.js 20, TypeScript, tsx runtime, jsforce (Salesforce), @slack/bolt (Slack), express (local API)  
**Running:** `npm run watch` or `npx tsx bin/star.ts watch`  
**Dev mode:** `npm run dev` (synthetic events, no credentials needed)

STAR Watch is the intelligence layer that sits invisibly across an organisation's tools. It requires no changes to how teams work — it observes, matches, acts, and proves.

#### Architecture

```
bin/star.ts                — CLI entry point (commander.js)
src/
├── daemon.ts              — Main process: starts watchers, routes events
├── event-store.ts         — Circular buffer of events + matches (in-memory)
├── api-server.ts          — Express HTTP API on :3001 (SOP app polls this)
├── matcher/
│   └── index.ts           — Event → SOP step matching pipeline
├── watchers/
│   ├── slack.ts           — Slack Socket Mode watcher
│   └── salesforce.ts      — Salesforce REST polling watcher
├── delivery/
│   └── slack.ts           — Slack Block Kit message delivery + sign-off handler
├── slack-client.ts        — Singleton Slack App instance (shared)
├── star-api.ts            — STAR API client (runs, steps, holons)
├── dev-mode.ts            — Synthetic event simulation (--dev flag)
├── config.ts              — ~/.star/config.json management
├── types.ts               — Shared TypeScript interfaces
└── logger.ts              — Coloured CLI output
```

#### Configuration (`~/.star/config.json`)

```json
{
  "avatar": "OASIS_ADMIN",
  "token": "<JWT from STAR API>",
  "apiBase": "http://localhost:5001",
  "org": "oasis",
  "connectors": {
    "slack": {
      "token": "xoxb-...",
      "appToken": "xapp-...",
      "deliverTo": "<channel-id>",
      "channels": []
    },
    "salesforce": {
      "instanceUrl": "https://yourorg.my.salesforce.com",
      "username": "user@org.com",
      "accessToken": "<OAuth token from `sf org login web`>",
      "securityToken": "<reset from SF settings>",
      "objects": ["Opportunity", "Lead", "Case", "Task"]
    }
  },
  "sops": { "autoLoad": true, "orgId": "oasis" },
  "escalation": {
    "defaultChannel": "slack",
    "requireSignOff": true,
    "notifyOnAutoComplete": true
  }
}
```

#### The Matching Pipeline

When any event arrives (from Slack, Salesforce, or any future connector), it passes through a three-stage pipeline:

**Stage 1 — Candidate filtering**  
Find SOP steps from active runs that could plausibly match. Filters on: connector type matching event source, OR steps with trigger conditions defined.

**Stage 2 — Semantic scoring**  
For each candidate, call `POST /api/braid/match` (STAR API) with the event payload and step context. BRAID returns a confidence score (0–1) and reasoning text. If BRAID is unavailable, a keyword fallback function compares event text against trigger condition values.

**Stage 3 — Action routing**  
Based on confidence and step configuration:

| Confidence | requiresSignOff | Action |
|-----------|-----------------|--------|
| ≥ 0.92 | false | `auto_complete` — step marked done, proof holon written |
| ≥ 0.75 | true or any | `escalate` — Slack card sent requesting human sign-off |
| 0.50–0.74 | any | `soft_notify` — informational only |
| < 0.50 | any | `log_only` — stored in event log, no action |

#### Slack Delivery

When a step is escalated, STAR Watch sends a Slack Block Kit card:

```
┌────────────────────────────────────────────┐
│ ★ SOP Step Detected                        │
│                                            │
│ Opportunity "STAR Watch Live Demo"         │
│ moved to Closed Won in Salesforce          │
│                                            │
│ Matches step: "Trigger customer onboarding"│
│ SOP: Enterprise Customer Onboarding v3     │
│ Confidence: 87%                            │
│                                            │
│  [Sign Off]    [Flag Deviation]            │
└────────────────────────────────────────────┘
```

Clicking **Sign Off** immediately updates the card (instant UI feedback), then asynchronously calls `POST /api/workflow/runs/{id}/steps/{stepId}/signoff` and writes an immutable proof holon to STARNET.

#### Local HTTP API (`:3001`)

Star-watch exposes a simple HTTP API on `localhost:3001` so the SOP app can display a live feed without polling the STAR API.

| Endpoint | Description |
|----------|-------------|
| `GET /status` | Uptime, connector list, event/match counts |
| `GET /events?limit=20` | Recent raw events from all connectors |
| `GET /matches?limit=20` | Events that matched SOP steps (with confidence, action) |

---

### 6.3 STAR API (Backend)

**Location:** `ONODE/NextGenSoftware.OASIS.API.ONODE.WebAPI/`  
**Stack:** .NET 8, ASP.NET Core, MongoDB  
**Running:** `http://localhost:5001`

The STAR API is the central backend, providing authentication, workflow persistence, execution management, and holon creation.

#### Existing Endpoints (pre-existing)

| Method | Route | Purpose |
|--------|-------|---------|
| `POST` | `/api/avatar/authenticate` | JWT login |
| `POST` | `/api/workflow/save` | Save WorkflowDefinition as Holon |
| `GET` | `/api/workflow/{holonId}` | Load a workflow |
| `GET` | `/api/workflow/my` | List my workflows |
| `GET` | `/api/workflow/public` | List public templates |
| `POST` | `/api/workflow/execute` | Execute a workflow inline |
| `GET` | `/api/workflow/proof/{id}/verify` | Verify a proof holon |
| `POST` | `/api/Holons/create` | Create any holon type |

#### New Endpoints (built in this session)

| Method | Route | Purpose |
|--------|-------|---------|
| `POST` | `/api/workflow/runs/start` | Start a new live SOP run |
| `GET` | `/api/workflow/runs` | List all active runs (org-scoped) |
| `GET` | `/api/workflow/execution/{id}` | Poll execution state (UI polling) |
| `POST` | `/api/workflow/execution/{id}/steps/{stepId}/complete` | Mark step done |
| `POST` | `/api/workflow/runs/{id}/steps/{stepId}/signoff` | Record human sign-off |
| `POST` | `/api/braid/match` | Semantic event→step matching |

#### New Files (built in this session)

```
Controllers/
├── WorkflowController.cs        — Extended with 5 new run endpoints
└── BraidController.cs           — New: BRAID match with keyword fallback

Models/Workflow/
└── WorkflowRunModels.cs         — DTOs: WorkflowRun, RunStepDto, etc.

Interfaces/
└── IWorkflowRunStore.cs         — Run store contract

Services/
└── WorkflowRunStore.cs          — Thread-safe in-memory run state
                                   (holon-backed in next phase)
```

#### BRAID Controller Design

The `BraidController` is designed for a clean swap from stub to real when BRAID API access is provisioned:

```csharp
// If BRAID:BaseUrl is set in appsettings.json → forwards to real BRAID service
// Otherwise → keyword matching fallback

// appsettings.json:
// "BRAID": {
//   "BaseUrl": "https://braid.oasis.earth",
//   "ApiKey":  "sk-..."
// }
```

One config key switches the entire matching engine from keyword-based to full semantic AI. No other code changes needed.

---

### 6.4 STARNET (On-Chain Proof Layer)

STARNET is the OASIS blockchain layer that makes the audit trail immutable and trustless. When a SOP step is completed — whether auto-completed by STAR Watch or signed off by a human — a `SOPStepCompletionHolon` is written to STARNET.

#### Proof Holon Structure

```json
{
  "name": "SOPStepCompletionHolon",
  "description": "STAR Watch — welcome_email_sent completed. sop-enterprise-onboarding · avatar-user",
  "holonSubType": 0,
  "createOptions": {
    "starnetHolon": {},
    "starnetdna": {
      "name": "SOPStepCompletionHolon",
      "description": "Immutable proof of SOP step completion recorded by STAR Watch",
      "starnetHolonType": "SOPStepCompletion"
    },
    "checkIfSourcePathExists": false,
    "customCreateParams": {
      "runId": "run-live-demo",
      "sopId": "sop-enterprise-onboarding",
      "stepId": "welcome_email_sent",
      "completedBy": "avatar-max-gershfield",
      "autoCompleted": false,
      "confidence": 0.92,
      "timestamp": "2026-03-29T03:24:14.000Z",
      "evidence": { "source": "slack", "excerpt": "sent the welcome email" }
    }
  }
}
```

The holon ID returned is permanent, publicly verifiable, and cannot be modified. This is what makes the audit trail trustless — not just a database entry that an admin could edit, but a record on a distributed ledger.

---

## 7. End-to-End Process Flow

### Example: Enterprise Customer Onboarding

**Day 0 — SOP is created**

1. Ops lead opens the SOP app at `/authoring`
2. Types: *"Enterprise customer onboarding: send welcome email, add to Slack, provision licences, schedule kick-off, complete go-live checklist, get sign-off from account director"*
3. BRAID generates a structured 6-step SOP with trigger conditions (e.g. step 1 triggers on "welcome email" in Slack) and sign-off requirements (step 6 requires Account Director sign-off)
4. Ops lead reviews, adjusts step names, saves → WorkflowDefinition written to STARNET as a Holon. ID: `wf-abc123`

**Day 1 — Deal closes in Salesforce**

5. Salesperson moves Opportunity "Acme Corp" to "Closed Won" in Salesforce
6. STAR Watch detects the stage change within 8 seconds (polling)
7. Matcher scores it against active runs → no active onboarding run for Acme yet
8. Customer success manager opens the SOP app, selects "Enterprise Customer Onboarding", clicks "Start Run"
9. STAR API creates a `WorkflowRun` with `executionId: exec-xyz789`, all steps in `Pending` state

**Day 1, 30 minutes later — Step 1 auto-completes**

10. CS manager sends Slack message: *"Hey team — welcome email is out to Acme"*
11. STAR Watch (Slack watcher) receives the message via Socket Mode
12. Matcher runs: keyword match on "welcome email" → scores 0.85 against step 1
13. Action: `escalate` (step is marked `requiresSignOff: false` but confidence is borderline)  
    → Actually: `auto_complete` (confidence ≥ 0.75, no sign-off required)
14. STAR Watch calls `POST /api/workflow/execution/exec-xyz789/steps/step-1/complete`
15. Proof holon written to STARNET: `SOPStepCompletionHolon` with `autoCompleted: true`, `confidence: 0.85`
16. SOP Runner UI (if CS manager has it open) auto-advances to Step 2 within 3 seconds (polling)

**Day 2 — Step 6 requires sign-off**

17. CS manager completes steps 2–5 manually via the Runner, or they are auto-detected
18. Step 6 "Go-live sign-off" is reached — `requiresSignOff: true`
19. STAR Watch sends a Slack Block Kit card to the Account Director:
    ```
    ★ Sign-off required: "Go-live sign-off"
    Enterprise Customer Onboarding — Acme Corp
    [Sign Off]  [Flag Deviation]
    ```
20. Account Director clicks **Sign Off**
21. Slack card updates immediately: "Signed off by [Account Director]"
22. `POST /api/workflow/runs/exec-xyz789/steps/step-6/signoff` called
23. Proof holon written: `completedBy: avatar-account-director`, `autoCompleted: false`
24. Run status → `Completed`. Full audit trail: 6 steps, 6 holons, all timestamped, all identity-linked.

**Week 4 — SOP evolves**

25. SOP Intel shows: Step 3 (licence provisioning) takes an average of 2.3 days, consistently longer than the SOP target of 1 day
26. BRAID recommends: *"Add a reminder trigger at T+24h if provisioning is not confirmed. Consider adding IT as a mandatory step assignee."*
27. Ops lead opens the SOP in the builder, adds an escalation rule to step 3, saves as version 2
28. All new runs use v2. Historical runs remain on v1 (full version history on STARNET).

---

## 8. Integrations

### Slack

**Mechanism:** Socket Mode (bidirectional WebSocket — no public webhook required)  
**Auth:** Bot Token (`xoxb-...`) + App-Level Token (`xapp-...`)  
**What it watches:** All messages in channels where the bot is invited  
**What it delivers:** Block Kit cards with interactive buttons for sign-off  
**Setup:** `star connect slack` then add tokens to `~/.star/config.json`

**How to invite the bot to a channel:**
```
/invite @holonic_sop
```

**Example trigger conditions for Slack steps:**
```json
"triggerConditions": [
  { "field": "text", "operator": "contains", "value": "welcome email" },
  { "field": "text", "operator": "contains", "value": "sent the intro" }
]
```

### Salesforce

**Mechanism:** REST API polling every 8 seconds (streaming API available but disabled for reliability)  
**Auth:** OAuth access token from `sf org login web` — no Connected App required for development  
**What it watches:** Opportunity, Lead, Case, Task — configurable  
**What it delivers:** Stage change events, record updates, status changes  
**Setup:** `sf org login web --alias star-watch` then access token auto-stored in config

**Example trigger conditions for Salesforce steps:**
```json
"triggerConditions": [
  { "field": "StageName", "operator": "changed_to", "value": "Closed Won" },
  { "field": "objectType", "operator": "equals", "value": "Opportunity" }
]
```

**Demo script:** `SOP/star-watch/demo-salesforce.sh`  
Creates a new Opportunity and walks it through Prospecting → Qualification → Proposal → Closed Won, demonstrating real-time detection at each stage.

### Connectors Planned (Architecture Ready)

| Connector | Mechanism | Use Cases |
|-----------|-----------|-----------|
| HubSpot | Webhook / REST polling | Deal stage, contact activity |
| Jira | Webhook | Ticket status changes, PR merges |
| GitHub | Webhook | PR merged, deployment complete |
| Email | IMAP / SendGrid inbound | Confirmation emails, client replies |
| Microsoft Teams | Bot Framework | Same as Slack, different delivery |
| Notion | API polling | Document updates, database changes |
| Zendesk | Webhook | Ticket resolved, CSAT received |

---

## 9. The Proof Holon — Immutable Audit Trail

Every completed SOP step produces a `SOPStepCompletionHolon` on STARNET. This is the core trust primitive of the system.

### What it proves

- **Who** completed the step (OASIS Avatar ID — persistent, cross-platform identity)
- **When** (timestamp, millisecond precision)
- **How** (auto-completed by AI detection, or manually signed off by a named human)
- **Confidence** (if auto-completed, the AI confidence score)
- **Evidence** (the source signal — Slack message excerpt, Salesforce record ID, etc.)
- **Which run** (links to the specific SOP execution)
- **Which version** (links to the exact version of the SOP in effect at the time)

### Why it cannot be faked

The holon is written to STARNET — the OASIS distributed ledger. Once written:
- It cannot be modified
- It cannot be deleted
- It is replicated across multiple nodes
- It is cryptographically linked to previous holons
- Verification is available via `GET /api/workflow/proof/{id}/verify`

This means that in an audit, instead of saying "we followed the process" and presenting a spreadsheet that could have been filled in retrospectively, a company can present a chain of STARNET holon IDs with verifiable timestamps and avatar signatures. Regulators, auditors, and counterparties can verify each one independently.

---

## 10. API Reference

### Authentication

All protected endpoints require:
```
Authorization: Bearer <JWT>
```

Get a token:
```bash
curl -X POST http://localhost:5001/api/avatar/authenticate \
  -H "Content-Type: application/json" \
  -d '{"username": "OASIS_ADMIN", "password": "Uppermall1!"}'
```

### Workflow Endpoints

#### Save a SOP
```
POST /api/workflow/save
Body: { "workflow": { WorkflowDefinition } }
Returns: { "result": { WorkflowDefinition with id } }
```

#### Start a Run
```
POST /api/workflow/runs/start
Body: {
  "workflowHolonId": "guid",   // or inline workflow
  "orgId": "oasis",
  "contextId": "SF-OPP-12345"  // links to external record
}
Returns: { "result": { executionId, steps[], status, startedAt } }
```

#### Poll Run State
```
GET /api/workflow/execution/{executionId}
Returns: { "result": { executionId, status, currentStep, steps[], completedAt } }
```

#### Complete a Step
```
POST /api/workflow/execution/{executionId}/steps/{stepId}/complete
Body: {
  "completedBy": "avatar-id",
  "note": "optional note",
  "evidence": ["url1", "url2"],
  "autoCompleted": true,
  "confidence": 0.87
}
```

#### Sign Off a Step
```
POST /api/workflow/runs/{runId}/steps/{stepId}/signoff
Body: {
  "avatarId": "avatar-account-director",
  "channel": "slack",
  "note": "Approved — all criteria met"
}
Returns: { "result": { signedBy, signedAt, proofHolonId } }
```

#### BRAID Match
```
POST /api/braid/match
Body: {
  "event": {
    "source": "slack",
    "action": "message",
    "payload": { "text": "sent the welcome email to Acme" }
  },
  "candidates": [
    {
      "id": "step-1",
      "name": "Send welcome email",
      "triggerConditions": [
        { "field": "text", "operator": "contains", "value": "welcome email" }
      ]
    }
  ],
  "runContext": { "runId": "exec-xyz789", "sopName": "Enterprise Onboarding" }
}
Returns: { "result": { "stepId": "step-1", "confidence": 0.87, "reasoning": "..." } }
```

### STAR Watch Local API

```
GET http://localhost:3001/status
GET http://localhost:3001/events?limit=20
GET http://localhost:3001/matches?limit=20
```

---

## 11. Running the System Locally

### Prerequisites

- .NET 8 SDK
- Node.js 20+
- MongoDB running locally
- Salesforce CLI (`npm install -g @salesforce/cli`)

### Step 1 — Start the STAR API

```bash
cd ONODE/NextGenSoftware.OASIS.API.ONODE.WebAPI
dotnet run
# API available at http://localhost:5001
# Swagger at http://localhost:5001/swagger
```

### Step 2 — Authenticate STAR Watch

```bash
# Re-auth if token has expired (JWT TTL is ~15 minutes)
TOKEN=$(curl -s -X POST http://localhost:5001/api/avatar/authenticate \
  -H "Content-Type: application/json" \
  -d '{"username":"OASIS_ADMIN","password":"Uppermall1!"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['result']['jwtToken'])")

python3 -c "
import json
with open('/Users/maxgershfield/.star/config.json') as f: cfg = json.load(f)
cfg['token'] = '$TOKEN'
with open('/Users/maxgershfield/.star/config.json', 'w') as f: json.dump(cfg, f, indent=2)
print('Token refreshed')
"
```

### Step 3 — Start STAR Watch

```bash
cd SOP/star-watch

# Production mode (real Slack + Salesforce)
npm run watch

# Dev mode (synthetic events, no credentials)
npm run dev

# Verbose (see all events, not just matches)
npx tsx bin/star.ts watch --verbose
```

### Step 4 — Start the SOP App

```bash
cd SOP/sop-app
npm run dev
# Open http://localhost:5176
```

### Step 5 — Authenticate in the SOP App

Username: `OASIS_ADMIN`  
Password: `Uppermall1!`

---

## 12. Demo Scripts

### Salesforce Demo

Walks an Opportunity through all sales stages, showing real-time detection in STAR Watch and the SOP app live feed.

```bash
cd SOP/star-watch
bash demo-salesforce.sh
```

Expected output in STAR Watch:
```
event  salesforce · stage_changed_to_qualification · [user]
event  salesforce · stage_changed_to_proposal_price_quote · [user]
event  salesforce · stage_changed_to_closed_won · [user]
match  "Trigger customer onboarding" — 85% · auto_complete
ok     Proof holon written to STARNET: [holon-id]
```

### Slack Demo

Send these messages to the Slack channel where `@holonic_sop` is invited:

```
"sent the welcome email"         → auto-completes step 1
"added client to Slack"          → auto-completes step 2
"licences have been provisioned" → auto-completes step 3
"going live today"               → escalates step 4 (requires sign-off)
```

The last message triggers a Block Kit card in Slack with Sign Off / Flag Deviation buttons.

### Dev Mode Demo (no credentials)

```bash
cd SOP/star-watch
npm run dev
```

Outputs synthetic events every 5 seconds and shows the full matching pipeline without any external connections.

---

## 13. Roadmap

### Phase 1 — Foundation (COMPLETE)
- [x] SOP App UI (authoring, runner, intel, connections)
- [x] STAR API workflow endpoints (save, execute, proof)
- [x] STAR Watch daemon architecture
- [x] Slack watcher (Socket Mode)
- [x] Salesforce watcher (OAuth + REST polling)
- [x] BRAID match endpoint with keyword fallback
- [x] Run management API (start, poll, complete, sign-off)
- [x] Slack Block Kit delivery with interactive sign-off
- [x] Proof holon writing to STARNET
- [x] Live activity feed in SOP app
- [x] Dev mode for zero-credential demos

### Phase 2 — Intelligence (Next)
- [ ] Real BRAID semantic matching (awaiting API access)
- [ ] SOP Intel: deviation detection, timing analysis, AI recommendations
- [ ] Avatar linking (map Slack/SF users to OASIS Avatars)
- [ ] Email watcher (IMAP / SendGrid)
- [ ] HubSpot watcher
- [ ] Run state persistence to Holons (currently in-memory)
- [ ] JWT auto-refresh in STAR Watch

### Phase 3 — Scale
- [ ] Multi-tenant SOP management (per-org isolation)
- [ ] SOP template marketplace
- [ ] Mobile push notifications for sign-off requests
- [ ] Microsoft Teams delivery adapter
- [ ] Jira / GitHub watchers
- [ ] Salesforce sidebar embed (see SOP status inside SF)
- [ ] Regulatory report generation (SOC2, ISO27001 evidence packs)

### Phase 4 — Platform
- [ ] SOP Builder SDK (embed authoring in any app)
- [ ] STAR Watch as a managed cloud service (no CLI required)
- [ ] BRAID fine-tuning on org-specific process data
- [ ] Cross-org benchmarking (how does your onboarding compare to industry?)
- [ ] SOP NFTs (trade and licence proven operating procedures)

---

## 14. Commercial Opportunity

### Primary Market: Customer Success & Operations Teams

Companies with 50–5000 employees that have:
- Complex recurring processes (onboarding, compliance, fund operations)
- Tools like Salesforce, Slack, HubSpot already in use
- Audit or compliance obligations
- High cost of process deviation (customer churn, regulatory fine, reputational damage)

### Pricing Model (Proposed)

| Tier | Target | Price | Includes |
|------|--------|-------|---------|
| Starter | SMB (1–50 users) | $299/mo | 5 SOPs, Slack + 1 connector, basic audit |
| Professional | Mid-market (50–500) | $999/mo | Unlimited SOPs, all connectors, full audit trail |
| Enterprise | Large org (500+) | Custom | White-label, dedicated STARNET, SLA, regulatory packs |

### The Beachhead: Blue Collar / Field Operations

A parallel opportunity exists in industries with hourly workers and physical processes — construction, logistics, facilities management, food production. Here the SOP compliance problem is even more acute (safety, certification, legal liability) and the existing tools are even less connected.

See: `Docs/BLUE_COLLAR_TIMECARDS_STAR_INTEGRATION.md` for the detailed integration plan connecting timecard/scheduling systems to holonic SOPs for this vertical.

### Key Differentiators for Sales

1. **No workflow change required** — teams use the tools they already have
2. **AI does the work** — observation, matching, and completion happen automatically
3. **Trustless audit trail** — blockchain-verified, regulator-ready
4. **Gets smarter over time** — BRAID learns from real process data
5. **Built on OASIS** — the only platform with persistent cross-application identity (Avatars), enabling compliance that follows a person across every tool they use

---

*Built on the OASIS Platform by the STAR team.*  
*Repository: `max-build4` branch, `OASIS_CLEAN`*  
*Contact: max@oasis.earth*
