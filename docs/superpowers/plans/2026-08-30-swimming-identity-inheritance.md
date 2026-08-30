# Swimming Identity Inheritance — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure the swimming identity model into a generic base `app_user` + role subtypes (`swimmer`, `captain`) across the design spec, the Mermaid `.mmd` diagram, and the Database Explorer.

**Architecture:** Class-table (table-per-type) inheritance. Consolidate person attributes onto base `app_user`; make `swimmer` and `captain` thin 1-1 subtypes via a unique `user_id`; re-point `captain_club` to `captain`. Apply the same change to three consistency-linked artifacts; the Explorer's app JS stays byte-identical (only its `SCHEMA`/`RELATIONSHIPS` data changes).

**Tech Stack:** Markdown spec; Mermaid `erDiagram` + `@mermaid-js/mermaid-cli` (`mmdc`); the self-contained Explorer HTML (data-driven JS); headless Chrome for a boot check.

**Spec:** `docs/superpowers/specs/2026-08-30-swimming-identity-inheritance-design.md` (amends `docs/superpowers/specs/2026-08-30-swimming-database-design.md`)

## Global Constraints

- **DB model / docs only** — no backend/EF code.
- **Net result:** 24 tables (add `captain`), 31 relationships. New enum `captain_type` (head | assistant).
- **The change, exactly:** base `app_user` gains person columns (`name_en, name_ar, national_id, gender, dob, phone, avatar_initials, email, password_hash`) and loses `display_name` + `swimmer_id`; `swimmer` gains `user_id (FK,U → identity.app_user.id)` and loses those person columns (keeps `uid, club_id, championship_club_id, blood_type, timestamps`); new `captain` subtype (`id, user_id FK,U, captain_type, created_at`); `captain_club` re-points `user_id` → `captain_id (FK,U → identity.captain.id)`.
- **Relationship delta:** keep `app_user ||--o| swimmer`; add `app_user ||--o| captain`; add `captain ||--o{ captain_club`; remove `app_user ||--o{ captain_club`. All authorship FKs stay on base `app_user`.
- **Schema placement:** `captain` in the **identity** schema; `swimmer` stays in **swimmers**.
- **Explorer:** only the `<script id="appdata">` block changes — the app module JS must remain untouched.
- **mermaid-cli:** puppeteer's auto Chrome is broken here; `mmdc` is installed globally. Every render call uses `PUPPETEER_EXECUTABLE_PATH="C:/Program Files/Google/Chrome/Application/chrome.exe" mmdc -i IN -o OUT`. `.diagram-check.*` is gitignored — never commit it.
- **Commit** after each task; do NOT push. Repo: `C:\Users\envnt\Desktop\Kheprx.Swmming`.

---

### Task 1: Amend the base design spec

**Files:**
- Modify: `docs/superpowers/specs/2026-08-30-swimming-database-design.md` (§5 Identity & Clubs, §6 Swimmers, §10 enums, §11 relationships)

**Interfaces:**
- Consumes: the new table definitions from the identity-inheritance spec §3–§8.
- Produces: a base spec whose identity model matches the new design — the authority Tasks 2 & 3 mirror.

- [ ] **Step 1: Rewrite the `app_user` table (§5)**

Replace the `app_user` row list with the base-user columns from the identity spec §3 (in the base spec's existing `| column | type | notes |` format): `id` PK; `username` UK; `email` UK (null); `password_hash` (null); `name_en`; `name_ar` (null); `national_id` varchar(14) UK; `gender` enum (null); `dob` (null); `phone` (null); `avatar_initials` (null); `role` enum; `is_first_login`; `active_club_id` FK→club (null); `created_at`. Remove `display_name` and `swimmer_id`.

- [ ] **Step 2: Rewrite the `swimmer` table (§6) and add the `captain` table (§5)**

`swimmer` (§6) → `id` PK; `user_id` FK,U → identity.app_user (1-1; person attrs on app_user); `uid` UK; `club_id` FK→club; `championship_club_id` FK→club (null); `blood_type`; `created_at`; `updated_at`. Remove `name_en, name_ar, national_id, gender, dob, phone, avatar_initials`.
Add a `captain` table to §5: `id` PK; `user_id` FK,U → identity.app_user; `captain_type` enum (null); `created_at`.
Change `captain_club` (§5): `captain_id` FK,U → identity.captain (was `user_id` → app_user); keep `club_id` FK,U; UK(captain_id, club_id).

- [ ] **Step 3: Update enums (§10) and relationships (§11)**

§10 — add `captain_type` (head | assistant). §11 — reflect: `app_user 1-1 swimmer` (via swimmer.user_id); add `app_user 1-1 captain`; `captain 1-N captain_club` (was app_user); update the table/relationship counts to **24 tables / 31 relationships**.

- [ ] **Step 4: Verify internal consistency**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
S=docs/superpowers/specs/2026-08-30-swimming-database-design.md
grep -q "name_en" <(grep -A20 'app_user' "$S") && echo "app_user has person attrs"
grep -qi "swimmer_id" <(grep -A20 'app_user' "$S") && echo "STILL HAS swimmer_id (bad)" || echo "app_user drops swimmer_id: OK"
grep -q "captain_type" "$S" && echo "captain_type enum present"
grep -q "captain_id" "$S" && echo "captain_club re-pointed: OK"
grep -qE "user_id.*app_user" "$S" && echo "swimmer.user_id present"
```
Expected: `app_user has person attrs`, `app_user drops swimmer_id: OK`, `captain_type enum present`, `captain_club re-pointed: OK`, `swimmer.user_id present`. No `STILL HAS swimmer_id (bad)`.

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/specs/2026-08-30-swimming-database-design.md
git commit -m "docs(spec): restructure identity to base user + swimmer/captain subtypes"
```

---

### Task 2: Update the Mermaid `.mmd` diagram

**Files:**
- Modify: `docs/references/swimming-database-diagram.mmd`

**Interfaces:**
- Consumes: the base spec (Task 1) as the authority.
- Produces: a diagram with 24 entities / 31 relationships reflecting the new identity model.

- [ ] **Step 1: Replace the `app_user`, `swimmer`, and `captain_club` entity blocks and add `captain`**

Replace the three existing entity blocks with these, and add the new `captain` block (place it among the identity entities):
```
    app_user {
        uuid id PK
        varchar username UK
        varchar email UK
        text password_hash
        varchar name_en
        varchar name_ar
        varchar(14) national_id UK
        gender gender
        date dob
        varchar phone
        varchar(4) avatar_initials
        user_role role
        boolean is_first_login
        uuid active_club_id FK
        timestamptz created_at
    }
    captain {
        uuid id PK
        uuid user_id FK,UK
        captain_type captain_type
        timestamptz created_at
    }
    captain_club {
        uuid id PK
        uuid captain_id FK,UK
        uuid club_id FK,UK
    }
```
```
    swimmer {
        uuid id PK
        uuid user_id FK,UK
        varchar uid UK
        uuid club_id FK
        uuid championship_club_id FK
        varchar(3) blood_type
        timestamptz created_at
        timestamptz updated_at
    }
```

- [ ] **Step 2: Update the relationship lines**

- REMOVE: `app_user ||--o{ captain_club : "assigned to"`
- ADD: `app_user ||--o| captain : "is"`
- ADD: `captain ||--o{ captain_club : "assigned"`
- Keep `app_user ||--o| swimmer : "logs in as"` (now realized via swimmer.user_id), `club ||--o{ captain_club : "managed by"`, `club ||--o{ app_user : "active club of"`, and both `club ||--o{ swimmer` lines.

- [ ] **Step 3: Validate render + counts**

```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
PUPPETEER_EXECUTABLE_PATH="C:/Program Files/Google/Chrome/Application/chrome.exe" mmdc -i docs/references/swimming-database-diagram.mmd -o docs/references/.diagram-check.svg && echo "RENDER OK"
echo "entities: $(grep -cE '^\s+[a-z_]+ \{' docs/references/swimming-database-diagram.mmd)"
echo "relationships: $(grep -cE '\|\|--o[\{|]' docs/references/swimming-database-diagram.mmd)"
grep -qE '^\s+captain \{' docs/references/swimming-database-diagram.mmd && echo "captain present"
grep -A10 '^\s\+swimmer \{' docs/references/swimming-database-diagram.mmd | grep -q 'user_id FK,UK' && echo "swimmer.user_id present"
grep -A16 '^\s\+app_user \{' docs/references/swimming-database-diagram.mmd | grep -qi 'swimmer_id' && echo "app_user STILL has swimmer_id (bad)" || echo "app_user drops swimmer_id: OK"
```
Expected: `RENDER OK`; entities `24`; relationships `31`; `captain present`; `swimmer.user_id present`; `app_user drops swimmer_id: OK`.

- [ ] **Step 4: Commit**

```bash
git add docs/references/swimming-database-diagram.mmd
git commit -m "docs(erd): apply identity base+subtype model to the diagram"
```

---

### Task 3: Update the Database Explorer data

**Files:**
- Modify: `docs/references/swimming-database-diagram.html` (only the `<script id="appdata">` block: `const SCHEMA` + `const RELATIONSHIPS`)

**Interfaces:**
- Consumes: the base spec (Task 1) + `.mmd` (Task 2).
- Produces: the Explorer reflecting the new identity model; app JS untouched.

- [ ] **Step 1: Update the `identity` schema tables in `SCHEMA`**

Replace `app_user` and `captain_club`, and add `captain`, in the `identity` schema array:
```
{name:"app_user",note:"base person + login + role (all users)",cols:[
  {c:"id",t:"uuid",k:"PK",n:false},
  {c:"username",t:"varchar",k:"U",n:true},
  {c:"email",t:"varchar",k:"U",n:true},
  {c:"password_hash",t:"text",k:"",n:true,note:"null when no login"},
  {c:"name_en",t:"varchar",k:"",n:false},
  {c:"name_ar",t:"varchar",k:"",n:true},
  {c:"national_id",t:"varchar(14)",k:"U",n:false},
  {c:"gender",t:"gender",k:"",n:true,note:"male | female"},
  {c:"dob",t:"date",k:"",n:true},
  {c:"phone",t:"varchar",k:"",n:true},
  {c:"avatar_initials",t:"varchar(4)",k:"",n:true},
  {c:"role",t:"user_role",k:"",n:false,note:"headCoach | captain | swimmer"},
  {c:"is_first_login",t:"boolean",k:"",n:false,d:"true"},
  {c:"active_club_id",t:"uuid",k:"FK",fk:"identity.club.id",n:true},
  {c:"created_at",t:"timestamptz",k:"",n:false}]},
{name:"captain",note:"captain profile — person attrs on app_user",cols:[
  {c:"id",t:"uuid",k:"PK",n:false},
  {c:"user_id",t:"uuid",k:"FK,U",fk:"identity.app_user.id",n:false},
  {c:"captain_type",t:"captain_type",k:"",n:true,note:"head | assistant"},
  {c:"created_at",t:"timestamptz",k:"",n:false}]},
{name:"captain_club",note:"captain ↔ clubs (composite key)",cols:[
  {c:"id",t:"uuid",k:"PK",n:false},
  {c:"captain_id",t:"uuid",k:"FK",fk:"identity.captain.id",n:false},
  {c:"club_id",t:"uuid",k:"FK",fk:"identity.club.id",n:false},
  {c:"(captain_id, club_id)",t:"—",k:"U",n:false,note:"one row per captain+club"}]},
```

- [ ] **Step 2: Thin the `swimmer` table in the `swimmers` schema**

Replace the `swimmer` table with:
```
{name:"swimmer",note:"swimmer subtype — person attrs on app_user",cols:[
  {c:"id",t:"uuid",k:"PK",n:false},
  {c:"user_id",t:"uuid",k:"FK,U",fk:"identity.app_user.id",n:false},
  {c:"uid",t:"varchar",k:"U",n:false},
  {c:"club_id",t:"uuid",k:"FK",fk:"identity.club.id",n:false},
  {c:"championship_club_id",t:"uuid",k:"FK",fk:"identity.club.id",n:true},
  {c:"blood_type",t:"varchar(3)",k:"",n:true},
  {c:"created_at",t:"timestamptz",k:"",n:false},
  {c:"updated_at",t:"timestamptz",k:"",n:false}]},
```

- [ ] **Step 3: Update `RELATIONSHIPS`**

In the `RELATIONSHIPS` array: remove `{from:"identity.app_user",to:"identity.captain_club",...}`; add `{from:"identity.app_user",to:"identity.captain",card:"1-1",label:"is"}` and `{from:"identity.captain",to:"identity.captain_club",card:"1-N",label:"assigned"}`. Keep `{from:"identity.app_user",to:"swimmers.swimmer",card:"1-1",label:"logs in as"}`, `{from:"identity.club",to:"identity.captain_club",...}`, and `{from:"identity.club",to:"identity.app_user",...}`.

- [ ] **Step 4: Validate data + confirm app JS untouched + boot**

```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
H=docs/references/swimming-database-diagram.html
# appdata JS syntax:
awk '/<script id="appdata">/{f=1;next} f&&/<\/script>/{f=0;next} f' "$H" > /tmp/appdata.js && node --check /tmp/appdata.js && echo "APPDATA JS OK"
# counts:
echo "tables: $(grep -c 'name:"' "$H")  relationships: $(grep -c 'from:"' "$H")  schemas: $(grep -c 'schema:"' "$H")"
grep -q 'name:"captain"' "$H" && echo "captain present"
# app module JS unchanged (must stay byte-identical to the Electric template's app JS):
awk '/<script type="module">/{f=1;next} f&&/<\/script>/{f=0;next} f' "$H" > /tmp/swim-app.js
awk '/<script type="module">/{f=1;next} f&&/<\/script>/{f=0;next} f' "C:/Users/envnt/Desktop/Kheprx.Electric/docs/references/electric-erp-database-diagram.html" > /tmp/elec-app.js
diff -q /tmp/elec-app.js /tmp/swim-app.js && echo "app JS still verbatim (untouched)"
# headless boot:
timeout 45 "C:/Program Files/Google/Chrome/Application/chrome.exe" --headless=new --disable-gpu --no-sandbox --virtual-time-budget=12000 --dump-dom "file:///C:/Users/envnt/Desktop/Kheprx.Swmming/docs/references/swimming-database-diagram.html" 2>/dev/null | grep -oE 'id="footTables">[0-9]+|<svg' | sort | uniq -c
```
Expected: `APPDATA JS OK`; tables `24`, relationships `31`, schemas `6`; `captain present`; `app JS still verbatim (untouched)`; boot shows `footTables">24` and an `<svg`.

- [ ] **Step 5: Commit**

```bash
git add docs/references/swimming-database-diagram.html
git commit -m "docs(erd): apply identity base+subtype model to the Explorer data"
```

---

## Self-Review

**Spec coverage** (identity spec → task):
- §3 base `app_user` → Task 1 Step 1; Task 2 Step 1; Task 3 Step 1.
- §4 thinned `swimmer` → Task 1 Step 2; Task 2 Step 1; Task 3 Step 2.
- §5 new `captain` → Task 1 Step 2; Task 2 Step 1; Task 3 Step 1.
- §6 `captain_club` re-point → Task 1 Step 2; Task 2 Step 1; Task 3 Step 1.
- §7 `captain_type` enum → Task 1 Step 3; carried as a type name + note in Tasks 2/3.
- §8 relationship delta → Task 1 Step 3; Task 2 Step 2; Task 3 Step 3.
- §9 three-artifact update → Tasks 1/2/3 respectively. All spec sections map to tasks.

**Placeholder scan:** No "TBD/TODO/handle X". Every entity block, SCHEMA object, and relationship edit is given verbatim; column details for the base-spec markdown reference the identity spec's tables (the authoritative source) with explicit grep gates.

**Type/name consistency:** `app_user`, `swimmer`, `captain`, `captain_club`, `user_id`, `captain_id`, `captain_type`, `identity.app_user.id`, `identity.captain.id` are used identically across all three tasks. Cardinalities agree: `app_user 1-1 swimmer`/`1-1 captain` (`||--o|` in Mermaid, `1-1` in Explorer), `captain 1-N captain_club` (`||--o{` / `1-N`). Counts agree everywhere: 24 entities/tables, 31 relationships. The Explorer `k:"U"` vs Mermaid `UK` distinction is preserved (Explorer uses `U`, Mermaid uses `FK,UK`).
