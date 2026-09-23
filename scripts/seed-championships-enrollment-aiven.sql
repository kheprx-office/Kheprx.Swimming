-- seed-championships-enrollment-aiven.sql
-- Enrolls a handful of real swimmers into the 3 demo events from
-- seed-championships-aiven.sql so the Enrollment tab shows data.
-- Idempotent (ON CONFLICT on the unique (EventId, SwimmerId) index).
-- NOTE: EF column names are PascalCase -> must be double-quoted in Postgres.

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- The first 4 swimmers (deterministic order) go into the two dated events.
WITH pick AS (
  SELECT "Id" AS swimmer_id, row_number() OVER (ORDER BY "Id") AS rn
  FROM identity.swimmer_profile
)
INSERT INTO championships.championship_enrollment ("Id","EventId","SwimmerId")
SELECT gen_random_uuid(), ev.event_id, pick.swimmer_id
FROM (
  VALUES
    ('33333333-3333-3333-3333-333333333301'::uuid),   -- National Junior → 4 swimmers
    ('33333333-3333-3333-3333-333333333302'::uuid)    -- Regional Sprint → 4 swimmers
) AS ev(event_id)
JOIN pick ON pick.rn <= 4
WHERE EXISTS (SELECT 1 FROM championships.competition_event e WHERE e."Id" = ev.event_id)
ON CONFLICT ("EventId","SwimmerId") DO NOTHING;
-- Winter Open (…3303) intentionally left with zero enrollments.

COMMIT;

-- Verify:
--   SELECT e."NameEn", count(en.*) AS enrolled
--   FROM championships.competition_event e
--   LEFT JOIN championships.championship_enrollment en ON en."EventId" = e."Id"
--   GROUP BY e."NameEn" ORDER BY e."NameEn";
