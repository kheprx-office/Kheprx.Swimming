-- seed-competition-schedule-aiven.sql
-- Builds a demo Competition Days schedule for the National Junior event (…3301):
--   Day 1 — Heats (2023-11-15): 50m Freestyle, 100m Backstroke
--   Day 2 — Finals (2023-11-16): 100m Freestyle
-- Races resolve StrokeId/DistanceId by Code; assignments come from …3301's enrolled swimmers.
-- Idempotent. Inserts nothing if the event, strokes, distances, or enrollments are missing.
-- NOTE: EF column names are PascalCase -> must be double-quoted in Postgres.

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 1) Days (fixed ids so races/assignments reference them deterministically).
INSERT INTO championships.competition_day ("Id","EventId","LabelEn","LabelAr","DayDate")
SELECT v."Id", v."EventId", v."LabelEn", v."LabelAr", v."DayDate"
FROM (VALUES
  ('55555555-5555-5555-5555-555555550001'::uuid, '33333333-3333-3333-3333-333333333301'::uuid, 'Day 1 — Heats',  'اليوم الأول — التصفيات', DATE '2023-11-15'),
  ('55555555-5555-5555-5555-555555550002'::uuid, '33333333-3333-3333-3333-333333333301'::uuid, 'Day 2 — Finals', 'اليوم الثاني — النهائيات', DATE '2023-11-16')
) AS v("Id","EventId","LabelEn","LabelAr","DayDate")
WHERE EXISTS (SELECT 1 FROM championships.competition_event e WHERE e."Id" = v."EventId")
ON CONFLICT ("Id") DO NOTHING;

-- 2) Races (fixed ids). StrokeId/DistanceId resolved by Code; skipped if a lookup is absent.
INSERT INTO championships.race_session ("Id","DayId","StrokeId","DistanceId","ScheduledTime")
SELECT v."Id", v."DayId",
       (SELECT "Id" FROM reference.stroke   WHERE "Code" = v.stroke_code),
       (SELECT "Id" FROM reference.distance WHERE "Code" = v.distance_code),
       v."ScheduledTime"
FROM (VALUES
  ('66666666-6666-6666-6666-666666660001'::uuid, '55555555-5555-5555-5555-555555550001'::uuid, 'freestyle',  '50m',  TIME '09:00'),
  ('66666666-6666-6666-6666-666666660002'::uuid, '55555555-5555-5555-5555-555555550001'::uuid, 'backstroke', '100m', TIME '09:30'),
  ('66666666-6666-6666-6666-666666660003'::uuid, '55555555-5555-5555-5555-555555550002'::uuid, 'freestyle',  '100m', TIME '10:00')
) AS v("Id","DayId",stroke_code,distance_code,"ScheduledTime")
WHERE EXISTS (SELECT 1 FROM championships.competition_day d WHERE d."Id" = v."DayId")
  AND (SELECT "Id" FROM reference.stroke   WHERE "Code" = v.stroke_code)   IS NOT NULL
  AND (SELECT "Id" FROM reference.distance WHERE "Code" = v.distance_code) IS NOT NULL
ON CONFLICT ("Id") DO NOTHING;

-- 3) Assignments: put the first two enrolled swimmers of …3301 into the two Day-1 races.
WITH enrolled AS (
  SELECT en."SwimmerId" AS swimmer_id, row_number() OVER (ORDER BY en."SwimmerId") AS rn
  FROM championships.championship_enrollment en
  WHERE en."EventId" = '33333333-3333-3333-3333-333333333301'
)
INSERT INTO championships.race_assignment ("Id","RaceSessionId","SwimmerId")
SELECT gen_random_uuid(), r.race_id, enrolled.swimmer_id
FROM (VALUES
  ('66666666-6666-6666-6666-666666660001'::uuid),   -- 50m Freestyle heats
  ('66666666-6666-6666-6666-666666660002'::uuid)    -- 100m Backstroke heats
) AS r(race_id)
JOIN enrolled ON enrolled.rn <= 2
WHERE EXISTS (SELECT 1 FROM championships.race_session s WHERE s."Id" = r.race_id)
ON CONFLICT ("RaceSessionId","SwimmerId") DO NOTHING;

COMMIT;

-- Verify:
--   SELECT d."LabelEn", count(rs.*) AS races
--   FROM championships.competition_day d
--   LEFT JOIN championships.race_session rs ON rs."DayId" = d."Id"
--   WHERE d."EventId" = '33333333-3333-3333-3333-333333333301'
--   GROUP BY d."LabelEn" ORDER BY d."LabelEn";
