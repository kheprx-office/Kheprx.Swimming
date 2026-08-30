# Swimming Database ERD Diagram — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce `docs/references/swimming-database-diagram.html` — a self-contained page rendering the swimming system's ~23-table schema as a Mermaid `erDiagram`, in the style of the Electric ERD reference.

**Architecture:** Author the schema as canonical Mermaid source in `docs/references/swimming-database-diagram.mmd`, built up one module at a time and validated with mermaid-cli after each module. A final task wraps that validated source in a styled, self-contained HTML viewer (heading, intro, legend, module grouping) matching the Electric reference look.

**Tech Stack:** Mermaid `erDiagram` (crow's-foot ER notation), `@mermaid-js/mermaid-cli` (`mmdc`, run via `npx`) for headless validation/render, plain HTML/CSS + Mermaid JS for the viewer.

**Spec:** `docs/superpowers/specs/2026-08-30-swimming-database-design.md`

## Global Constraints

- **Deliverable is a diagram doc only** — no EF Core, no SQL DDL, no backend changes.
- **Single source of truth for columns is the spec.** Every table's exact columns/types/PK/FK/UK are in spec §5–§10. Transcribe them; do not invent or rename columns. This plan supplies the Mermaid format, worked examples, and the exact relationship lines (which are not in the spec as Mermaid).
- **Organization:** entities are grouped/ordered by module in this order — Reference, Identity & Clubs, Swimmers, Health & Records, Attendance, Championships.
- **Bilingual columns** appear as `*_en` / `*_ar`. **PK type is `uuid`.**
- **Cross-module references** are marked `FK` on the column and drawn as relationships, but there are no DB-enforced cross-module FKs (per spec §4) — this is a diagram, so all relationships are drawn in one graph regardless.
- **Authorship FKs** (`recorded_by` / `created_by` / `author_id` / `coach_id` → `app_user`) are shown as `FK` on their columns and noted in the legend, but their relationship lines are NOT drawn (drawing ~8 lines into `app_user` creates an unreadable hub). All other structural relationships ARE drawn.
- **Commit** after each task. Do NOT push (pushing is a separate, user-approved step).
- The repo is at `C:\Users\envnt\Desktop\Kheprx.Swmming`; the diagram files live in `docs/references/`.

### Mermaid column & relationship format (used by every task)

Entity block — one line per column, `type name KEYS`:
```
    swimmer {
        uuid id PK
        uuid club_id FK
        varchar name_en
        varchar name_ar
        varchar national_id UK
        date dob
    }
```
Keys are `PK`, `FK`, `UK`, or combos like `FK,UK`. For a composite unique constraint (e.g. `UK(session_id, swimmer_id)`), mark BOTH columns `FK,UK` and rely on the legend to explain composite UK. Use spec types verbatim; where a type has a size (`varchar(14)`, `numeric(5,1)`) keep it (Mermaid treats the type as an opaque token — parentheses are fine inside the type word if written without spaces, e.g. `varchar(14)`).

Relationship lines use crow's-foot with a verb label:
```
    club ||--o{ swimmer : "has"
    competition_event ||--o{ competition_day : "spans"
```
`||--o{` = one-to-many; `}o--o{` = many-to-many (only used implicitly via junction tables, which we model as explicit entities, so junctions use two one-to-many lines).

---

### Task 1: Diagram scaffold + Reference + Identity & Clubs modules

**Files:**
- Create: `docs/references/swimming-database-diagram.mmd`
- Create (throwaway, gitignored): `docs/references/.diagram-check.svg` (mmdc output; do not commit)

**Interfaces:**
- Produces: the `.mmd` file opening with `erDiagram` and containing entities `stroke`, `distance`, `club`, `app_user`, `captain_club` — later tasks append to this same file and reference these entities in relationships.

- [ ] **Step 1: Preflight — confirm mermaid-cli renders a trivial diagram**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
printf 'erDiagram\n    A {\n        uuid id PK\n    }\n' > docs/references/.diagram-check.mmd
npx -y @mermaid-js/mermaid-cli -i docs/references/.diagram-check.mmd -o docs/references/.diagram-check.svg
echo "exit=$?"; ls -la docs/references/.diagram-check.svg
```
Expected: `mmdc` installs on first run (may download a headless browser — allow a few minutes), exits 0, and writes an SVG. If mmdc cannot run in this environment, STOP and report BLOCKED with the exact error — the fallback is browser-render validation (Task 6 covers a browser check), but per-task validation needs mmdc.

- [ ] **Step 2: Create the diagram with the Reference + Identity & Clubs entities**

Create `docs/references/swimming-database-diagram.mmd` starting with `erDiagram`, then add these entities. Transcribe `stroke`, `distance` from spec §10 and `club`, `app_user`, `captain_club` from spec §5, EVERY column, using the format above. Worked example for one entity (transcribe the rest the same way):
```
erDiagram
    stroke {
        uuid id PK
        varchar code
        varchar name_en
        varchar name_ar
    }
    distance {
        uuid id PK
        varchar code
        integer meters
    }
    club {
        uuid id PK
        varchar name_en
        varchar name_ar
        varchar location_en
        varchar location_ar
        timestamptz created_at
    }
    app_user {
        uuid id PK
        varchar username UK
        varchar email
        varchar display_name
        user_role role
        varchar(4) avatar_initials
        boolean is_first_login
        uuid active_club_id FK
        uuid swimmer_id FK
        timestamptz created_at
    }
    captain_club {
        uuid id PK
        uuid user_id FK,UK
        uuid club_id FK,UK
    }
```

- [ ] **Step 3: Add the Identity & Clubs relationships**

Append after the entity blocks:
```
    app_user ||--o{ captain_club : "assigned to"
    club ||--o{ captain_club : "managed by"
    club ||--o| app_user : "active club of"
```

- [ ] **Step 4: Validate it renders and the entities are present**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
npx -y @mermaid-js/mermaid-cli -i docs/references/swimming-database-diagram.mmd -o docs/references/.diagram-check.svg && echo "RENDER OK"
grep -cE "^\s+(stroke|distance|club|app_user|captain_club) \{" docs/references/swimming-database-diagram.mmd
```
Expected: `RENDER OK`, and the grep prints `5` (all five entities defined).

- [ ] **Step 5: Ignore the throwaway check artifacts and commit**

```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
printf 'docs/references/.diagram-check.*\n' >> .gitignore
git add docs/references/swimming-database-diagram.mmd .gitignore
git commit -m "docs(erd): scaffold swimming DB diagram + Reference/Identity modules"
```

---

### Task 2: Swimmers module

**Files:**
- Modify: `docs/references/swimming-database-diagram.mmd`

**Interfaces:**
- Consumes: entities `club`, `app_user`, `stroke` from Task 1.
- Produces: entities `swimmer`, `guardian`, `medical_exam`, `body_measurement`, `swimmer_specialization`.

- [ ] **Step 1: Append the Swimmers entities**

Insert the five Swimmers entities from spec §6 (every column) before the relationship lines, using the column format. Worked example (`swimmer_specialization`; transcribe `swimmer`, `guardian`, `medical_exam`, `body_measurement` the same way from spec §6):
```
    swimmer_specialization {
        uuid swimmer_id FK,UK
        uuid stroke_id FK,UK
    }
```
Note: for `body_measurement`, the spec lists paired columns as `right_arm_cm / left_arm_cm` — write these as SEPARATE lines (`numeric(5,1) right_arm_cm`, `numeric(5,1) left_arm_cm`, etc.). Same for legs.

- [ ] **Step 2: Append the Swimmers relationships**

```
    club ||--o{ swimmer : "trains"
    club ||--o{ swimmer : "represents"
    app_user ||--o| swimmer : "logs in as"
    swimmer ||--o{ guardian : "has"
    swimmer ||--o{ medical_exam : "examined in"
    swimmer ||--o{ body_measurement : "measured in"
    swimmer ||--o{ swimmer_specialization : "specializes"
    stroke ||--o{ swimmer_specialization : "chosen in"
```

- [ ] **Step 3: Validate render + entities present**

```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
npx -y @mermaid-js/mermaid-cli -i docs/references/swimming-database-diagram.mmd -o docs/references/.diagram-check.svg && echo "RENDER OK"
grep -cE "^\s+(swimmer|guardian|medical_exam|body_measurement|swimmer_specialization) \{" docs/references/swimming-database-diagram.mmd
```
Expected: `RENDER OK` and grep prints `5`.

- [ ] **Step 4: Commit**

```bash
git add docs/references/swimming-database-diagram.mmd
git commit -m "docs(erd): add Swimmers module tables + relationships"
```

---

### Task 3: Health & Records module

**Files:**
- Modify: `docs/references/swimming-database-diagram.mmd`

**Interfaces:**
- Consumes: `swimmer` (Task 2), `app_user` (Task 1).
- Produces: `inbody_reading`, `medical_test`, `health_reading`, `observation`, `feedback_entry`.

- [ ] **Step 1: Append the Health & Records entities**

Transcribe the five entities from spec §7 (every column). Mark `recorded_by` / `created_by` / `author_id` columns as `FK` (authorship — no relationship line drawn, per Global Constraints). Worked example (`health_reading`):
```
    health_reading {
        uuid id PK
        uuid swimmer_id FK
        uuid medical_test_id FK
        numeric(8,2) value
        date reading_date
        uuid recorded_by FK
    }
```

- [ ] **Step 2: Append the Health & Records relationships** (structural only; authorship omitted)

```
    swimmer ||--o{ inbody_reading : "records"
    swimmer ||--o{ health_reading : "records"
    medical_test ||--o{ health_reading : "measured by"
    swimmer ||--o{ observation : "noted on"
    swimmer ||--o{ feedback_entry : "receives"
```

- [ ] **Step 3: Validate render + entities present**

```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
npx -y @mermaid-js/mermaid-cli -i docs/references/swimming-database-diagram.mmd -o docs/references/.diagram-check.svg && echo "RENDER OK"
grep -cE "^\s+(inbody_reading|medical_test|health_reading|observation|feedback_entry) \{" docs/references/swimming-database-diagram.mmd
```
Expected: `RENDER OK` and grep prints `5`.

- [ ] **Step 4: Commit**

```bash
git add docs/references/swimming-database-diagram.mmd
git commit -m "docs(erd): add Health & Records module tables + relationships"
```

---

### Task 4: Attendance module

**Files:**
- Modify: `docs/references/swimming-database-diagram.mmd`

**Interfaces:**
- Consumes: `club`, `stroke` (Task 1), `swimmer` (Task 2), `app_user` (Task 1).
- Produces: `attendance_session`, `attendance_record`.

- [ ] **Step 1: Append the Attendance entities**

Transcribe `attendance_session` and `attendance_record` from spec §8 (every column). `coach_id` is authorship (`FK`, no line). `attendance_record` has composite `UK(session_id, swimmer_id)` → mark both `session_id` and `swimmer_id` as `FK,UK`. Worked example:
```
    attendance_record {
        uuid id PK
        uuid session_id FK,UK
        uuid swimmer_id FK,UK
        attendance_status status
        text coach_note_en
        text coach_note_ar
    }
```

- [ ] **Step 2: Append the Attendance relationships**

```
    club ||--o{ attendance_session : "holds"
    stroke ||--o{ attendance_session : "grouped by"
    attendance_session ||--o{ attendance_record : "includes"
    swimmer ||--o{ attendance_record : "attends"
```

- [ ] **Step 3: Validate render + entities present**

```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
npx -y @mermaid-js/mermaid-cli -i docs/references/swimming-database-diagram.mmd -o docs/references/.diagram-check.svg && echo "RENDER OK"
grep -cE "^\s+(attendance_session|attendance_record) \{" docs/references/swimming-database-diagram.mmd
```
Expected: `RENDER OK` and grep prints `2`.

- [ ] **Step 4: Commit**

```bash
git add docs/references/swimming-database-diagram.mmd
git commit -m "docs(erd): add Attendance module tables + relationships"
```

---

### Task 5: Championships module

**Files:**
- Modify: `docs/references/swimming-database-diagram.mmd`

**Interfaces:**
- Consumes: `swimmer` (Task 2), `stroke`/`distance` (Task 1), `app_user` (Task 1).
- Produces: `competition_event`, `championship_enrollment`, `competition_day`, `race_session`, `race_assignment`, `race_result`.

- [ ] **Step 1: Append the Championships entities**

Transcribe the six entities from spec §9 (every column). `created_by` / `recorded_by` are authorship (`FK`, no line). Composite UKs: `championship_enrollment` UK(event_id, swimmer_id); `race_assignment` UK(race_session_id, swimmer_id); `race_result` UK(race_session_id, swimmer_id) — mark those pairs `FK,UK`. Worked example:
```
    race_result {
        uuid id PK
        uuid race_session_id FK,UK
        uuid swimmer_id FK,UK
        integer time_ms
        integer points
        integer rank
        boolean is_personal_best
        uuid recorded_by FK
    }
```

- [ ] **Step 2: Append the Championships relationships**

```
    competition_event ||--o{ championship_enrollment : "enrolls"
    swimmer ||--o{ championship_enrollment : "enrolled in"
    competition_event ||--o{ competition_day : "spans"
    competition_day ||--o{ race_session : "schedules"
    stroke ||--o{ race_session : "of stroke"
    distance ||--o{ race_session : "of distance"
    race_session ||--o{ race_assignment : "assigns"
    swimmer ||--o{ race_assignment : "swims in"
    race_session ||--o{ race_result : "produces"
    swimmer ||--o{ race_result : "achieves"
```

- [ ] **Step 3: Validate render + entities present + full relationship count**

```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
npx -y @mermaid-js/mermaid-cli -i docs/references/swimming-database-diagram.mmd -o docs/references/.diagram-check.svg && echo "RENDER OK"
grep -cE "^\s+(competition_event|championship_enrollment|competition_day|race_session|race_assignment|race_result) \{" docs/references/swimming-database-diagram.mmd
echo "entities:"; grep -cE "^\s+[a-z_]+ \{" docs/references/swimming-database-diagram.mmd
echo "relationships:"; grep -cE "\|\|--o[\{|]" docs/references/swimming-database-diagram.mmd
```
Expected: `RENDER OK`; first grep prints `6`; total entities `23`; relationships `30`.

- [ ] **Step 4: Commit**

```bash
git add docs/references/swimming-database-diagram.mmd
git commit -m "docs(erd): add Championships module tables + relationships"
```

---

### Task 6: Self-contained HTML viewer + completeness check

**Files:**
- Create: `docs/references/swimming-database-diagram.html`
- Read: `docs/references/swimming-database-diagram.mmd`, `docs/references/electric-erp-database-diagram.html` (for style cues)

**Interfaces:**
- Consumes: the completed, validated `.mmd` from Tasks 1–5.
- Produces: the final deliverable HTML page.

- [ ] **Step 1: Build the styled, self-contained viewer**

Create `docs/references/swimming-database-diagram.html` containing:
- A `<head>` with a title `Kheprx Swimming — Database ERD`, and CSS for a clean centered page (light background, readable sans-serif, a header block, and a legend box) echoing the Electric reference's overall look (open `electric-erp-database-diagram.html` briefly for palette/spacing cues; do not copy its embedded library wholesale).
- A header naming the system and listing the six clusters: **Reference, Identity & Clubs, Swimmers, Health & Records, Attendance, Championships**.
- A **legend** explaining: `PK` primary key, `FK` foreign key, `UK` unique (composite UKs mark both columns), crow's-foot `||--o{` = one-to-many, and the note: "Authorship columns (`recorded_by` / `created_by` / `author_id` / `coach_id`) reference `app_user`; shown as FK, lines omitted for readability. Cross-module references are logical (no DB-enforced FK) per the design."
- A `<pre class="mermaid">` element whose content is the ENTIRE text of `swimming-database-diagram.mmd` (copy it in verbatim).
- Mermaid initialization. Preferred: vendor `mermaid.min.js` into `docs/references/` and reference it locally for an offline, self-contained page like the Electric reference; if vendoring is impractical, load it from a CDN (`https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js`) and call `mermaid.initialize({ startOnLoad: true })`. State which you chose in the report.

- [ ] **Step 2: Validate the embedded diagram matches the source**

Run — confirm the HTML embeds the same entities as the validated source:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
echo "mmd entities:"; grep -cE "^\s+[a-z_]+ \{" docs/references/swimming-database-diagram.mmd
echo "html entities:"; grep -cE "^\s+[a-z_]+ \{" docs/references/swimming-database-diagram.html
```
Expected: both print `23` (the `<pre>` block carries the full diagram).

- [ ] **Step 3: Visual render check in a browser**

Open the HTML and confirm the diagram renders (all six clusters visible, no Mermaid error box). Use the project's browser/run tooling to load `docs/references/swimming-database-diagram.html` and capture a screenshot; confirm entities render and there is no red Mermaid parse-error panel. If a CDN was used and the environment is offline, note that the page needs network to render and (if required) vendor the library instead.

- [ ] **Step 4: Completeness review against the spec**

Confirm every table in spec §5–§10 appears in the diagram and every relationship in spec §11 is drawn (except the intentionally-omitted authorship lines). Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
for t in stroke distance club app_user captain_club swimmer guardian medical_exam body_measurement swimmer_specialization inbody_reading medical_test health_reading observation feedback_entry attendance_session attendance_record competition_event championship_enrollment competition_day race_session race_assignment race_result; do
  grep -qE "^\s+$t \{" docs/references/swimming-database-diagram.mmd || echo "MISSING: $t"
done; echo "check done"
```
Expected: only `check done` prints (no `MISSING` lines) — all 23 tables present.

- [ ] **Step 5: Commit**

```bash
git add docs/references/swimming-database-diagram.html docs/references/mermaid.min.js
git commit -m "docs(erd): add self-contained HTML viewer for the swimming DB diagram"
```
(Omit `mermaid.min.js` from the `git add` if you used the CDN instead of vendoring.)

---

## Self-Review

**Spec coverage** (spec → task):
- §5 Identity & Clubs (club, app_user, captain_club) → Task 1. §10 Reference (stroke, distance) → Task 1.
- §6 Swimmers (5 tables) → Task 2. §7 Health & Records (5) → Task 3. §8 Attendance (2) → Task 4. §9 Championships (6) → Task 5.
- §11 relationships → distributed across Tasks 1–5 (each relationship placed in the task where both endpoints exist); authorship lines intentionally omitted per Global Constraints, consistent with spec §4 ("shown as FK on columns").
- §13 diagram-doc requirements (self-contained HTML, Electric style, legend, module grouping, master erDiagram with PK/FK/UK + crow's-foot) → Task 6.
- §12 mock→schema mapping and §14 out-of-scope (RegistrationWizard/payment) require no diagram entities — nothing to draw; correctly absent.
All spec tables (23) and relationship groups map to a task. No gaps.

**Placeholder scan:** No "TBD/TODO". Column details are delegated to the spec (the required, authoritative input) by explicit section reference with a worked Mermaid example per module + the exact relationship lines given inline — this is DRY, not a placeholder: the plan never says "add the columns" without pointing to the exact source and showing the format. Relationship lines and validation commands are fully concrete.

**Type/name consistency:** Entity names are identical everywhere they appear (definition, relationships, grep checks, completeness loop): the 23 names in Task 6 Step 4's loop exactly match the five per-module sets in Tasks 1–5. Relationship endpoints only reference entities defined in the same or an earlier task (verified: `swimmer` used from Task 3 on is defined in Task 2; `stroke`/`distance` from Task 1). Composite-UK columns are consistently marked `FK,UK` in Tasks 1, 4, 5. Expected counts are consistent: 23 entities total, 30 drawn relationships.
