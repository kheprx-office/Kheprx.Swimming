# Swimming System — Database Design

**Date:** 2026-08-30
**Status:** Approved (design), pending spec review
**Repo:** `C:\Users\envnt\Desktop\Kheprx.Swmming`
**Domain source:** Magic Patterns swimming app (local snapshot, project `v7esayw7`, at `C:\Users\envnt\Desktop\4dba8937-ce3f-44bd-b093-515f10a5ea2b`)
**Style reference:** `C:\Users\envnt\Desktop\Kheprx.Electric\docs\references\electric-erp-database-diagram.html` (Mermaid `erDiagram` HTML)

## 1. Goal & deliverable

Design the relational database for the swimming club/academy system, organized to match the
modular-monolith backend already scaffolded, and produce a **Mermaid `erDiagram` HTML document** in
the Electric reference style that visualizes it.

**This spec's deliverable is the design + the diagram doc.** EF Core entities and migrations are
explicitly a later, separate effort — not part of this work.

## 2. Non-goals

- No EF Core entities, `DbContext`s, migrations, or backend wiring (deferred).
- No SQL DDL scripts (the chosen deliverable is the diagram, not runnable DDL).
- No swimming business logic, services, or endpoints.
- No changes to the existing Identity module's code (this design *aligns with* it; it does not modify it).

## 3. Decisions (locked)

| Axis | Decision |
|------|----------|
| Deliverable | Schema design (this spec) + Mermaid `erDiagram` HTML doc; implement later |
| Organization | By modular-monolith modules: Identity, Athlete, Health & Records, Attendance, Championships, + shared Reference |
| Domain source | Local Magic Patterns snapshot `v7esayw7` |
| Normalization | **Approach C (pragmatic hybrid)** — normalize the mock's duplicated shapes into canonical tables; keep genuinely-distinct structures (InBody, medical-test catalog, free-form observations) as their own tables |

## 4. Conventions

- **Primary key:** every table has `id uuid` (GUID), matching the Identity module's convention.
- **Bilingual columns:** `*_en` / `*_ar` wherever the domain carries an Arabic label (`nameAr`, etc.).
- **Timestamps:** `created_at timestamptz`, and `updated_at timestamptz` on mutable entities.
- **Authorship:** `recorded_by` / `created_by uuid` → `app_user.id` where a coach/headCoach authors data.
- **Module boundaries (important):** foreign keys are DB-enforced **only within a module**.
  Cross-module references (e.g. `swimmer_id` inside Attendance, `club_id` inside Athlete) are
  **logical/soft references by id** — each module owns its own schema and `DbContext`, exactly as
  Electric did. The ERD still draws these as relationships, marked as cross-module.
- **Derived, never stored** (computed at query time): roster `health_status`; reading `severity`
  (value vs `medical_test` bounds); attendance `month_rate`; event `participants` count; per-race
  ordinal ranking where the UI sorts. (An entered `rank` and `is_personal_best` ARE stored on
  `race_result` because coaches set them.)
- **Enums** are modeled as named enum types / check-constrained columns (see §10). `stroke` and
  `distance` are **reference tables** (not enums) because they are reused across modules and carry
  bilingual labels.

## 5. Module 1 — Identity

**app_user** — generic base user (class-table inheritance); absorbs person attributes from all role subtypes.
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| username | varchar | UK |
| email | varchar | UK (nullable) |
| password_hash | text | nullable; null when no login |
| name_en | varchar | |
| name_ar | varchar | nullable |
| gender_id | uuid | FK → reference.gender; nullable |
| dob | date | nullable |
| phone | varchar | nullable |
| role_id | uuid | FK → reference.role |
| is_first_login | boolean | default true |
| created_at | timestamptz | |

**captain_profile** — subtype for captain role (1-1 with app_user)
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| user_id | uuid | FK,U → identity.app_user.id |
| national_id | varchar(14) | UK |
| created_at | timestamptz | |

**head_coach_profile** — subtype for head coach role (1-1 with app_user)
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| user_id | uuid | FK,U → identity.app_user.id |
| national_id | varchar(14) | UK |
| created_at | timestamptz | |

**swimmer_profile** — subtype for swimmer role (1-1 with app_user); person attrs live on app_user.
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| user_id | uuid | FK,U → identity.app_user.id |
| uid | varchar | UK, e.g. `SW-2026-4KD91` |
| training_club_id | uuid | FK → club (training club) |
| represent_championship_club_id | uuid | FK → club (nullable) |
| created_at | timestamptz | |
| updated_at | timestamptz | |

## 6. Module 2 — Athlete

**guardian** — normalizes father/mother into rows
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| swimmer_id | uuid | FK → identity.swimmer_profile (cross-module) |
| relation | enum `guardian_relation` | father \| mother |
| name | varchar | |
| national_id | varchar(14) | |
| phone | varchar | |

**medical_exam** — dated fitness/vitals from registration
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| swimmer_id | uuid | FK → identity.swimmer_profile (cross-module) |
| exam_date | date | |
| internal_med_id | uuid | FK → reference.fitness_assessment |
| heart_assess_id | uuid | FK → reference.fitness_assessment |
| spine_assess_id | uuid | FK → reference.fitness_assessment |
| blood_type_id | uuid | FK → reference.blood_type (nullable) |
| hemoglobin | numeric(4,1) | g/dL |
| height_cm | numeric(5,1) | |
| weight_kg | numeric(5,1) | |
| created_at | timestamptz | |

**body_measurement** — dated physiological measurements (left/right split)
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| swimmer_id | uuid | FK → identity.swimmer_profile (cross-module) |
| measured_at | date | |
| right_arm_cm / left_arm_cm | numeric(5,1) | |
| right_leg_cm / left_leg_cm | numeric(5,1) | |
| torso_cm | numeric(5,1) | |
| bust_diameter_cm | numeric(5,1) | |
| waist_diameter_cm | numeric(5,1) | |

**swimmer_specialization** — swimmer ↔ stroke (many-to-many)
| column | type | notes |
|---|---|---|
| swimmer_id | uuid | FK → identity.swimmer_profile (cross-module) |
| stroke_id | uuid | FK → stroke (Reference) — cross-module |
| — | | UK(swimmer_id, stroke_id) |

## 7. Module 3 — Health & Records

**inbody_reading** — fixed body-composition metrics, per reading
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| swimmer_id | uuid | FK → identity.swimmer_profile (cross-module) |
| reading_date | date | |
| height_cm | numeric(5,1) | |
| weight_kg | numeric(5,1) | |
| fat_pct | numeric(4,1) | body fat % |
| muscle_pct | numeric(4,1) | muscle mass % |
| bone_density | numeric(4,2) | g/cm³ |
| body_density | numeric(4,2) | g/cm³ |
| recorded_by | uuid | FK → app_user (cross-module) |
| created_at | timestamptz | |

**medical_test** — headCoach-managed test catalog
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| name_en / name_ar | varchar | |
| unit | varchar | e.g. `g/dL` |
| lower_bound | numeric(8,2) | |
| upper_bound | numeric(8,2) | |
| created_by | uuid | FK → app_user (cross-module) |
| created_at | timestamptz | |

**health_reading** — a test result; `severity` computed vs bounds, not stored
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| swimmer_id | uuid | FK → identity.swimmer_profile (cross-module) |
| medical_test_id | uuid | FK → medical_test |
| value | numeric(8,2) | |
| reading_date | date | |
| recorded_by | uuid | FK → app_user (cross-module) |

**observation** — unifies `DataEntry` + `RecordRow` flags + registration medical flags (allergies/surgeries/chronic/autoimmune)
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| swimmer_id | uuid | FK → identity.swimmer_profile (cross-module) |
| category_id | uuid | FK → reference.observation_category |
| field_label | varchar | e.g. "Penicillin" |
| value | varchar | e.g. "Severe" |
| observed_date | timestamptz | |
| recorded_by | uuid | FK → app_user (cross-module) |

**feedback_entry** — coach performance evaluations
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| swimmer_id | uuid | FK → identity.swimmer_profile (cross-module) |
| rating | smallint | 1–5 |
| category | enum `feedback_category` | Technique \| Endurance \| Attitude \| Punctuality \| Other |
| comment | text | |
| author_id | uuid | FK → app_user (cross-module) |
| entry_date | date | |

## 8. Module 4 — Attendance

**attendance_session**
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| club_id | uuid | FK → club (cross-module) |
| session_date | date | |
| stroke_id | uuid | FK → stroke (nullable; the specialization group) |
| coach_id | uuid | FK → app_user (cross-module) |

**attendance_record** — per-swimmer status + required bilingual coach note (also serves the calendar `dayNotes`)
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| session_id | uuid | FK → attendance_session |
| swimmer_id | uuid | FK → identity.swimmer_profile (cross-module) |
| status | enum `attendance_status` | present \| late \| absent \| excused |
| coach_note_en | text | |
| coach_note_ar | text | |
| — | | UK(session_id, swimmer_id) |

## 9. Module 5 — Championships

**competition_event**
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| name_en / name_ar | varchar | |
| start_date / end_date | date | |
| location_en / location_ar | varchar | |
| status | enum `competition_status` | upcoming \| completed |
| created_by | uuid | FK → app_user (cross-module) |

**championship_enrollment** — event ↔ swimmer
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| event_id | uuid | FK → competition_event |
| swimmer_id | uuid | FK → identity.swimmer_profile (cross-module) |
| — | | UK(event_id, swimmer_id) |

**competition_day**
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| event_id | uuid | FK → competition_event |
| label_en / label_ar | varchar | e.g. "Day 1 — Heats" |
| day_date | date | |

**race_session** — a race slot; `race_name` derived = distance + stroke
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| day_id | uuid | FK → competition_day |
| stroke_id | uuid | FK → stroke (Reference) |
| distance_id | uuid | FK → distance (Reference) |
| scheduled_time | time | nullable |

**race_assignment** — who swims this race
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| race_session_id | uuid | FK → race_session |
| swimmer_id | uuid | FK → identity.swimmer_profile (cross-module) |
| — | | UK(race_session_id, swimmer_id) |

**race_result** — replaces `RaceResult` + `SessionResult` + `ChampionshipRace`
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| race_session_id | uuid | FK → race_session |
| swimmer_id | uuid | FK → identity.swimmer_profile (cross-module) |
| time_ms | integer | canonical finish time (ms); display `MM:SS.ss` derived |
| points | integer | FINA-style points |
| rank | integer | entered finishing rank |
| is_personal_best | boolean | |
| recorded_by | uuid | FK → app_user (cross-module) |
| — | | UK(race_session_id, swimmer_id) |

## 10. Reference (shared lookups) + enums

**stroke** — unifies the mock's `Specialization` and `Stroke` (IM = Medley); used by Athlete + Championships
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| code | varchar | freestyle \| backstroke \| butterfly \| breaststroke \| medley |
| name_en / name_ar | varchar | |

**distance**
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| code | varchar | `50m` … `10000m` (50, 100, 200, 400, 800, 1000, 1500, 5000, 7000, 7500, 10000) |
| name_en | varchar | |
| name_ar | varchar | |
| meters | integer | |

**club** — swimming club or academy
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| name_en | varchar | |
| name_ar | varchar | |
| created_at | timestamptz | |

_Seed rows — fixed Egyptian clubs (~70). English `name_en` transliterations are best-effort; correct as needed._

| # | name_en | name_ar | | # | name_en | name_ar |
|---|---|---|---|---|---|---|
| 1 | Al Ahly | الأهلي | | 36 | El Shams | الشمس |
| 2 | Zamalek | الزمالك | | 37 | El Nasr | النصر |
| 3 | Pyramids | بيراميدز | | 38 | El Obour | العبور |
| 4 | Al Ittihad Alexandria | الاتحاد السكندري | | 39 | El Merreikh | المريخ |
| 5 | Al Masry (Port Said) | المصري البورسعيدي | | 40 | Port Fouad | بورفؤاد |
| 6 | Ismaily | الإسماعيلي | | 41 | Eastern Company | إيسترن كومباني |
| 7 | Smouha | سموحة | | 42 | El Nogoom | النجوم |
| 8 | ENPPI | إنبي | | 43 | Gomhoreyet Shebin | جمهورية شبين |
| 9 | Wadi Degla | وادي دجلة | | 44 | Benha | بنها |
| 10 | ZED FC | زد إف سي | | 45 | El Plastic | البلاستيك |
| 11 | Ceramica Cleopatra | سيراميكا كليوباترا | | 46 | Sporting Alexandria | سبورتنج السكندري |
| 12 | Modern Sport | مودرن سبورت | | 47 | El Olympi | الأوليمبي |
| 13 | National Bank of Egypt | البنك الأهلي المصري | | 48 | El Hammam | الحمام |
| 14 | El Gouna | الجونة | | 49 | Damanhour | دمنهور |
| 15 | Pharco | فاركو | | 50 | Kafr El Sheikh | كفر الشيخ |
| 16 | Petrojet | بتروجت | | 51 | Damietta | دمياط |
| 17 | Ghazl El Mahalla | غزل المحلة | | 52 | Dekernes | دكرنس |
| 18 | Haras El Hodood | حرس الحدود | | 53 | Beni Ebeid | بني عبيد |
| 19 | Tala'ea El Gaish | طلائع الجيش | | 54 | Nabaroh | نبروه |
| 20 | Arab Contractors | المقاولون العرب | | 55 | El Minya | المنيا |
| 21 | Ismailia Electricity | كهرباء الإسماعيلية | | 56 | El Fayoum | الفيوم |
| 22 | El Tersana | الترسانة | | 57 | Misr El Makkasa | مصر المقاصة |
| 23 | Tanta | طنطا | | 58 | Beni Suef Telecom | تليفونات بني سويف |
| 24 | El Sekka El Hadeed (Railways) | السكة الحديد | | 59 | Aluminium | الألومنيوم |
| 25 | Aswan | أسوان | | 60 | Kima Aswan | كيما أسوان |
| 26 | El Qanah | القناة | | 61 | Tahta | طهطا |
| 27 | La Viena | لافيينا | | 62 | Luxor | الأقصر |
| 28 | Abu Qir Fertilizers | أبو قير للأسمدة | | 63 | El Nasr Mining | النصر للتعدين |
| 29 | Telecom Egypt | المصرية للاتصالات | | 64 | Asyut Cement | أسمنت أسيوط |
| 30 | Asyut Petroleum | بترول أسيوط | | 65 | Shoban Muslimeen Qena | شبان مسلمين قنا |
| 31 | El Mansoura | المنصورة | | 66 | El Badari | البداري |
| 32 | Baladeyet El Mahalla | بلدية المحلة | | 67 | Aviation Club | نادي الطيران |
| 33 | El Dakhleya | الداخلية | | 68 | Shooting Club | نادي الصيد |
| 34 | El Entag El Harby | الإنتاج الحربي | | 69 | Palm Hills | بالم هيلز |
| 35 | Suez Team | منتخب السويس | | 70 | 6th of October Club | نادي 6 أكتوبر |

**gender** — gender lookup
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| code | varchar | e.g. `male`, `female` |
| name_en | varchar | |
| name_ar | varchar | nullable |

**role** — user role lookup
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| code | varchar | e.g. `headCoach`, `captain`, `swimmer` |
| name_en | varchar | |
| name_ar | varchar | nullable |

**blood_type** — blood type lookup
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| code | varchar | e.g. `O+`, `A-`, `AB+` |
| name_en | varchar | |
| name_ar | varchar | nullable |

**observation_category** — health observation category lookup
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| code | varchar | e.g. `allergy`, `surgery`, `chronic`, `autoimmune`, `composition`, `flag`, `other` |
| name_en | varchar | |
| name_ar | varchar | nullable |

**fitness_assessment** — medical exam fitness result lookup
| column | type | notes |
|---|---|---|
| id | uuid | PK |
| code | varchar | e.g. `fit`, `unfit` |
| name_en | varchar | |
| name_ar | varchar | nullable |

**Enum types:** `guardian_relation` (father|mother),
`feedback_category` (Technique|Endurance|Attitude|Punctuality|Other),
`attendance_status` (present|late|absent|excused), `competition_status` (upcoming|completed).

## 11. Relationships (cardinalities)

**29 tables / 34 relationships.**

- gender 1—* app_user; role 1—* app_user; blood_type 1—* medical_exam; observation_category 1—* observation; fitness_assessment 1—* medical_exam.
- club 1—* swimmer_profile; club 1—* attendance_session.
- app_user 1—1 swimmer_profile (via swimmer_profile.user_id); app_user 1—1 captain_profile (via captain_profile.user_id); app_user 1—1 head_coach_profile (via head_coach_profile.user_id).
- swimmer_profile 1—* {guardian, medical_exam, body_measurement, inbody_reading, health_reading, observation, feedback_entry, attendance_record, race_assignment, race_result}.
- swimmer_profile *—* stroke (swimmer_specialization); swimmer_profile *—* competition_event (championship_enrollment).
- medical_test 1—* health_reading.
- attendance_session 1—* attendance_record.
- competition_event 1—* competition_day 1—* race_session 1—* {race_assignment, race_result}.
- stroke 1—* {swimmer_specialization, attendance_session, race_session}; distance 1—* race_session.
- app_user 1—* authored rows (recorded_by/created_by/author_id/coach_id).

## 12. Mock → schema mapping (Approach C)

| Mock shape(s) | Schema |
|---|---|
| `RaceResult` + `SessionResult` + `ChampionshipRace` | one `race_result` (championship history = query) |
| `MedicalTest` + `HealthReading` | kept as catalog + reading; `severity` derived |
| `InbodyReading` | kept as `inbody_reading` (fixed metric set) |
| `RecordRow`/`RecordGroup` flags + `DataEntry` + registration allergy/surgery/chronic/autoimmune | one `observation` |
| father/mother columns | `guardian` rows |
| left/right arm & leg | explicit `*_arm_cm` / `*_leg_cm` columns |
| `Specialization` + `Stroke` | one `stroke` reference table |
| roster `healthStatus`, `severity`, `monthRate`, `participants` | derived (queries), not stored |

## 13. Diagram document spec

Produce `docs/references/swimming-database-diagram.html` — a self-contained HTML page rendering the
schema as a Mermaid **`erDiagram`**, matching the Electric reference's look:

- **One master `erDiagram`** containing all tables and relationships (so cross-module links render),
  with entities visually grouped/ordered by module and a legend; PK/FK/UK markers on columns and
  crow's-foot relationship notation with verb labels (e.g. `club ||--o{ swimmer : "has"`).
- Cross-module relationships styled/annotated as such (e.g. a note or dashed convention), reflecting
  §4's soft-FK boundary rule.
- Include column types as in this spec. Bilingual columns shown as `name_en` / `name_ar`.
- Mermaid may be vendored or loaded via CDN; a self-contained (offline) page is preferred, like the
  reference. A short heading/intro naming the system and the 5 modules + Reference.

## 14. Assumptions & open items

- PK type is `uuid` to match the Identity module; if that module uses a different key type, the
  implementation aligns to it.
- `app_user` is the existing Identity user extended conceptually; the diagram shows the swimming-
  relevant columns only.
- Time stored as `time_ms` (canonical) with display formatting derived; if the implementation
  prefers storing the `MM:SS.ss` text too, that is an additive change.
- Health-reading `severity` and roster `health_status` use the mock's formulas (value vs bounds;
  worst active flag) — computed in the query/service layer, documented for the implementers.
- **Resolved — out of scope: the public `RegistrationWizard`.** The mock's second, public-facing
  sign-up wizard (personal info → `swimming_level` → `preferred_schedule` → payment method) is a
  distinct enrollment/payment funnel. Decision: **out of scope** for this database — the operational
  core (clubs/swimmers/health/attendance/championships) stands on its own. No `registration_request`,
  `swimming_level`/`preferred_schedule` enums, or payment tables are modeled.
