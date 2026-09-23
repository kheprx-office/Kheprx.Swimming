-- seed-championships-aiven.sql
-- Seeds reference.competition_status (2 statuses, fixed ids) and the 3 demo
-- championships.competition_event rows from the design mock. Idempotent (safe to re-run).
-- NOTE: EF column names are PascalCase -> must be double-quoted in Postgres.

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 1) Competition statuses (fixed ids so events reference them deterministically).
INSERT INTO reference.competition_status ("Id","Code","NameEn","NameAr") VALUES
  ('22222222-2222-2222-2222-222222222201','upcoming','Upcoming','قادمة'),
  ('22222222-2222-2222-2222-222222222202','completed','Completed','مكتملة')
ON CONFLICT ("Code") DO NOTHING;

-- 2) Three demo events (fixed ids), created by a head coach.
WITH coach AS (
  SELECT "Id" AS id FROM identity.app_user
  WHERE "RoleId" = (SELECT "Id" FROM reference.role WHERE "Code" = 'head_coach')
  ORDER BY "Id" LIMIT 1
),
st AS (
  SELECT
    (SELECT "Id" FROM reference.competition_status WHERE "Code"='upcoming')  AS upcoming,
    (SELECT "Id" FROM reference.competition_status WHERE "Code"='completed') AS completed
)
INSERT INTO championships.competition_event
  ("Id","NameEn","NameAr","StartDate","EndDate","LocationEn","LocationAr","StatusId","CreatedBy")
SELECT v."Id", v."NameEn", v."NameAr", v."StartDate", v."EndDate", v."LocationEn", v."LocationAr", v."StatusId", (SELECT id FROM coach)
FROM (
  VALUES
    ('33333333-3333-3333-3333-333333333301'::uuid, 'National Junior Championship', 'بطولة الناشئين الوطنية',
     DATE '2023-11-15', DATE '2023-11-16', 'Cairo Olympic Pool', 'حمام السباحة الأولمبي بالقاهرة', (SELECT upcoming FROM st)),
    ('33333333-3333-3333-3333-333333333302'::uuid, 'Regional Sprint Meet', 'لقاء السرعة الإقليمي',
     DATE '2023-10-20', DATE '2023-10-20', 'Alexandria Sports Center', 'مركز الإسكندرية الرياضي', (SELECT completed FROM st)),
    ('33333333-3333-3333-3333-333333333303'::uuid, 'Winter Open Championship', 'بطولة الشتاء المفتوحة',
     DATE '2024-01-20', DATE '2024-01-21', 'Giza Aquatic Center', 'مركز الجيزة المائي', (SELECT upcoming FROM st))
) AS v("Id","NameEn","NameAr","StartDate","EndDate","LocationEn","LocationAr","StatusId")
WHERE (SELECT id FROM coach) IS NOT NULL
ON CONFLICT ("Id") DO NOTHING;

COMMIT;

-- Verify:
--   SELECT count(*) FROM championships.competition_event;
--   SELECT e."NameEn", s."Code" FROM championships.competition_event e
--     JOIN reference.competition_status s ON s."Id" = e."StatusId" ORDER BY e."StartDate" DESC;
