# How Q2P Works With the STAR Templating Engine

This doc explains how the **STAR OAPP templating engine** works and how the **Q2P template** fits in—including where it aligns and where it’s currently C#-focused.

---

## 1. STAR templating lifecycle

| Step | CLI / API | What happens |
|------|-----------|----------------|
| **Create** | `oapp template create` | Wizard creates an empty folder + `OAPPTemplateDNA.json`. You add your template files (any language/platform). |
| **Publish** | `oapp template publish` | Folder is packed into a `.oapptemplate` file; optionally uploaded to STARNET so others can find it. |
| **Install** | `oapp template install` | User installs a template (from file or STARNET) into `OAPPTemplates/Installed/`. |
| **Create from template** | `oapp create` → choose template | Wizard asks for OAPP name, namespace, optional parent; then **STAR.Light** runs. |

When creating an OAPP from a template, the flow is:

1. **Copy** – The installed template folder is copied to the new OAPP path (e.g. `DefaultOAPPsSourcePath/MyShop/`). All files (e.g. storefront, medusa, config) are copied as-is.
2. **Tag mapping** – CLI discovers custom tags in the template and asks the user for values:
   - **`[[TAG]]`** – Custom text tags: user enters the value to substitute (e.g. `[[SITE_NAME]]` → "Alton Towers Shop").
   - **`{{TAG}}`** – Holon tags: user maps to a field from CelestialBody meta data (for C#/DNA-based OAPPs).
3. **STAR.Light** – Called with `OAPPName`, `OAPPTemplateId`, `metaTagMappings` (and optionally `metaHolonTagMappings`). It:
   - Ensures the OAPP folder exists (already done by copy).
   - Calls **ApplyOAPPTemplate**, which does **string replacement** of tags in the copied files.

So the “templating engine” is: **copy template → collect tag values → replace tags in files**.

---

## 2. Tag system

### Built-in tags (replaced by STAR.Light)

| Tag | Replaced with |
|-----|----------------|
| `{OAPPNAME}` | OAPP name (e.g. "My Q2P Shop") |
| `{OAPPNAMESPACE}` | Genesis namespace (e.g. "MyQ2PShop") |
| `{CELESTIALBODY}` | CelestialBody type name (for C# DNA templates) |
| `{CELESTIALBODYVAR}` | CelestialBody variable name (camelCase) |
| `{ZOME1}`, `{HOLON1}`, `{HOLON1_STRINGPROPERTY1}`, … | From CelestialBodyMetaDataDNA (C# Zomes/Holons) |
| `{INITCUSTOMTAGHOLONS}` | Generated holon init code when using `{{HolonTag}}` |
| `{LIBRARYUSINGSTATEMENTS}`, `{LIBRARYMETHODSTUBS}` | Library integration (GeneratedProxies) |

### Custom tags (user-provided at create time)

| Tag form | Purpose |
|----------|--------|
| **`[[CUSTOM_TAG_NAME]]`** | Replaced with whatever text the user enters (e.g. `[[SITE_NAME]]` → "Alton Towers Merch"). Shown in the “map meta tags” wizard. |
| **`{{CUSTOM_TAG_NAME}}`** | Replaced with a **holon node** from CelestialBodyMetaDataDNA (e.g. `{{MyField}}` → `myHolon.Instance.MyField`). Used for C# DNA–driven OAPPs. |

Q2P’s `oapp-template/README.md` mentions:

- `{OAPPNAME}` – shop name  
- `{OAPPNAMESPACE}` – C# namespace (if applicable)  
- `[[SITE_NAME]]` – custom: site display name  

So for Q2P you rely on **built-in** `{OAPPNAME}` and **custom** `[[SITE_NAME]]`.

---

## 3. Where replacement runs (important for Q2P)

In the current codebase:

- **ApplyOAPPTemplate** (in `Star.cs`) only processes:
  - **`*.cs`** – line-by-line replacement of `{OAPPNAME}`, `{OAPPNAMESPACE}`, `[[key]]`, `{{holon}}`, etc.
  - **`*.csproj`** – same idea for project files.

- **GetCustomTagsFromTemplate** (in `OAPPs.cs`) only **scans**:
  - **`*.cs`** – to find `[[tag]]` and `{{tag}}` so the wizard can ask for mappings.

So:

- **Discovery** of `[[SITE_NAME]]` and similar only happens if they appear in a **.cs** file.
- **Replacement** of `[[SITE_NAME]]` and `{OAPPNAME}` only happens in **.cs** and **.csproj** files.

The Q2P template is **Next.js + Medusa** (`.tsx`, `.ts`, `.json`, `.env`, etc.). So with the current engine:

- Tags in **.tsx / .ts / .json / .env** are **not** discovered and **not** replaced.
- The template is still **fully usable as a copy**: creating an OAPP from the Q2P template gives a full copy of the storefront + medusa; the builder then has to set `SITE_NAME`, `NEXT_PUBLIC_SITE_NAME`, etc. manually in env and config.

---

## 4. How Q2P can work with STAR today

**Option A – Copy-only (current behaviour)**  
- Use `oapp create` → select Q2P template.  
- STAR copies the whole template folder (storefront, medusa, `OAPPTemplateDNA.json`) to the new OAPP path.  
- No tag replacement in JS/TS/env files.  
- Builder renames the app / sets env vars (e.g. `SITE_NAME`, Medusa URL, Stripe) by hand.  
- **Quest link** is unchanged: store still calls `GET /api/quest/entitlements` when that API exists; template doesn’t need to be C# for that.

**Option B – Make tags work for Q2P (engine extension)**  
To have `[[SITE_NAME]]` and `{OAPPNAME}` applied inside the Next.js/Medusa tree:

1. **Extend GetCustomTagsFromTemplate** – Scan not only `*.cs` but also `*.ts`, `*.tsx`, `*.json`, `*.env`, `.env.example`, etc. (or a configurable list of extensions), so that tags in the storefront/medusa are discovered and the wizard can ask for values.
2. **Extend ApplyOAPPTemplate** – Run the same replacement logic (at least for `{OAPPNAME}` and `[[key]]` from `metaTagMappings`) over those file types (and optionally skip binary/lock files).  
Then the Q2P template can safely use `[[SITE_NAME]]` in e.g. `.env.example` or a small config file that the app reads.

**Option C – Stub .cs file in Q2P template**  
- Add a single `.cs` file in the template that only contains comments or a string with `[[SITE_NAME]]` and `{OAPPNAME}`.  
- Tags get discovered and the user can map them.  
- Replacement would still only run in that `.cs` file today, so you’d need a build step or script that reads that generated .cs (or a generated config) and writes env/config for Next.js/Medusa—possible but brittle.

Recommendation: **Option B** (extend discovery + replacement to the extensions used by Q2P) so the same STAR templating engine cleanly supports both C# OAPPs and JS/TS/Next.js templates like Q2P.

---

## 5. End-to-end: template → quest-linked store

1. **Template** – Q2P is an OAPP template (folder + `OAPPTemplateDNA.json`); publish with `oapp template publish`, install with `oapp template install`.
2. **Create shop** – Builder runs `oapp create`, picks “Q2P Shop”, enters name and e.g. `[[SITE_NAME]]`. With current code: copy only; with Option B: tags in TS/env replaced.
3. **Deploy** – Builder deploys storefront + Medusa (e.g. Railway), configures env (OASIS ONODE URL, Stripe, etc.).
4. **Quest link** – Storefront already calls `lib/oasis.ts` → `GET /api/quest/entitlements` (when ONODE exposes it). No templating change needed for that; it’s the same store code in every created OAPP.  
   So: **STAR templating** = clone + customise the **shop** (name, env, optional branding). **Quest-to-physical** = ONODE registry + proofs + entitlement API; the template just ships the client that calls those APIs.

---

## 6. References in code

| Location | What it does |
|----------|----------------|
| **STAR-Mac-Build/STAR ODK/NextGenSoftware.OASIS.STAR.CLI.Lib/OAPPTemplates.cs** | Template wizard text; built-in and custom tag list. |
| **STAR-Mac-Build/STAR ODK/NextGenSoftware.OASIS.STAR.CLI.Lib/OAPPs.cs** | `MapCustomMetaTagsToTemplate` (discovers `[[tag]]`), `GetCustomTagsFromTemplate` (scans **.cs** only), create-from-template flow calling `STAR.LightAsync`. |
| **STAR-Mac-Build/STAR ODK/NextGenSoftware.OASIS.STAR/Star.cs** | `Light` / `LightInternalAsync` (init folder from template, then apply); `ApplyOAPPTemplate` (replacement in **.cs** and **.csproj** only); `InitOAPPFolderAsync` (copy installed template). |
| **Q2P/oapp-template/OAPPTemplateDNA.json** | Template id, name, description, `templateType: "q2p-shop"`, features (e.g. `quest-proof-discounts`). |
| **Q2P/oapp-template/README.md** | Documents `{OAPPNAME}`, `{OAPPNAMESPACE}`, `[[SITE_NAME]]` for STAR.Light. |

---

## 7. Summary

- **STAR templating** = create/publish/install OAPP templates, then create an OAPP from a template = **copy** installed template + **replace** tags. Tag replacement is currently implemented only for **.cs** and **.csproj**; custom tags are discovered only in **.cs**.
- **Q2P** is a non-C# template (Next.js + Medusa). It works with STAR today as **copy-only**; `[[SITE_NAME]]` and `{OAPPNAME}` in TS/env are not replaced unless the engine is extended (Option B).
- **Linking the store to quests** does not depend on the templating engine: it depends on ONODE (Quest Registry, proofs, `GET /api/quest/entitlements`). The Q2P template already includes the storefront code that calls that API; every shop created from the template will use it once ONODE supports it.
