-- seed-race-results-aiven.sql
-- Records finish times for one seeded race on the National Junior event (...3301), leaving
-- another seeded race empty, so both tabs are populated after implementation:
--   50m Freestyle heats (...660001): its 2 assigned swimmers get times  -> Results tab
--   100m Backstroke heats (...660002): left with no results             -> Finished races tab
-- Depends on seed-competition-schedule-aiven.sql having run (races + assignments exist).
-- Idempotent. Inserts nothing if the race, its assignments, or an app_user are missing.
-- NOTE: EF column names are PascalCase -> must be double-quoted in Postgres.

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- recorded_by is not surfaced in the UI; any existing app_user is fine.
WITH recorder AS (
  SELECT "Id" AS id FROM identity.app_user ORDER BY "Id" LIMIT 1
),
-- The 50m Freestyle race's assigned swimmers, ordered deterministically.
entrants AS (
  SELECT a."SwimmerId" AS swimmer_id,
         row_number() OVER (ORDER BY a."SwimmerId") AS rn
  FROM championships.race_assignment a
  WHERE a."RaceSessionId" = '66666666-6666-6666-6666-666666660001'
)
INSERT INTO championships.race_result
  ("Id","RaceSessionId","SwimmerId","TimeMs","Points","IsPersonalBest","RecordedBy")
SELECT
  v."Id",
  '66666666-6666-6666-6666-666666660001'::uuid,
  entrants.swimmer_id,
  v."TimeMs",
  0,
  true,
  (SELECT id FROM recorder)
FROM (VALUES
  ('77777777-7777-7777-7777-777777770001'::uuid, 1, 24560),   -- 0:24.56
  ('77777777-7777-7777-7777-777777770002'::uuid, 2, 25890)    -- 0:25.89
) AS v("Id", rn, "TimeMs")
JOIN entrants ON entrants.rn = v.rn
WHERE EXISTS (SELECT 1 FROM recorder)
ON CONFLICT ("RaceSessionId","SwimmerId") DO NOTHING;

COMMIT;

-- verify (expect r=...660001 -> 2 results, r=...660002 -> 0):
--   SELECT rs."Id" AS session, count(rr.*) AS results
--   FROM championships.race_session rs
--   JOIN championships.competition_day d ON d."Id" = rs."DayId"
--   LEFT JOIN championships.race_result rr ON rr."RaceSessionId" = rs."Id"
--   WHERE d."EventId" = '33333333-3333-3333-3333-333333333301'
--   GROUP BY rs."Id" ORDER BY rs."Id";
