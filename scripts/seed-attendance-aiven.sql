-- seed-attendance-aiven.sql
-- Seeds reference.attendance_status (4 statuses, fixed ids) and ~6 weeks of weekday
-- attendance.attendance_record rows for EVERY swimmer. Idempotent (safe to re-run).
-- NOTE: EF column names are PascalCase -> must be double-quoted in Postgres.

BEGIN;

-- pgcrypto provides gen_random_uuid() (built-in on PG13+, this is a safety net).
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 1) Attendance statuses (fixed ids so records reference them deterministically).
INSERT INTO reference.attendance_status ("Id","Code","NameEn","NameAr") VALUES
  ('11111111-1111-1111-1111-111111111101','present','Present','حاضر'),
  ('11111111-1111-1111-1111-111111111102','late','Late','متأخر'),
  ('11111111-1111-1111-1111-111111111103','absent','Absent','غائب'),
  ('11111111-1111-1111-1111-111111111104','excused','Excused','بعذر')
ON CONFLICT ("Code") DO NOTHING;

-- 2) ~6 weeks of weekday records for every swimmer.
WITH days AS (
  SELECT d::date AS session_date
  FROM generate_series(CURRENT_DATE - INTERVAL '42 days', CURRENT_DATE, INTERVAL '1 day') AS d
  WHERE EXTRACT(ISODOW FROM d) < 6            -- Mon..Fri only
),
coach AS (
  SELECT "Id" AS id FROM identity.app_user
  WHERE "RoleId" = (SELECT "Id" FROM reference.role WHERE "Code" = 'head_coach')
  ORDER BY "Id" LIMIT 1
),
sids AS (
  SELECT
    (SELECT "Id" FROM reference.attendance_status WHERE "Code"='present') AS present,
    (SELECT "Id" FROM reference.attendance_status WHERE "Code"='late')    AS late,
    (SELECT "Id" FROM reference.attendance_status WHERE "Code"='absent')  AS absent,
    (SELECT "Id" FROM reference.attendance_status WHERE "Code"='excused') AS excused
),
hashes AS (
  SELECT sp."Id" AS sid, d.session_date,
         abs(hashtextextended(sp."Id"::text || d.session_date::text, 0)) % 10 AS h
  FROM identity.swimmer_profile sp CROSS JOIN days d
)
INSERT INTO attendance.attendance_record
  ("Id","SwimmerId","SessionDate","StatusId","RecordedBy","CoachNoteEn","CoachNoteAr")
SELECT
  gen_random_uuid(),
  hs.sid,
  hs.session_date,
  CASE hs.h
    WHEN 0 THEN sids.absent
    WHEN 1 THEN sids.excused
    WHEN 2 THEN sids.late
    ELSE sids.present
  END,
  (SELECT id FROM coach),
  CASE WHEN hs.h = 1 THEN 'Excused — family notified, travel.' END,
  CASE WHEN hs.h = 1 THEN 'بعذر — تم إبلاغ العائلة، سفر.' END
FROM hashes hs
CROSS JOIN sids
WHERE (SELECT id FROM coach) IS NOT NULL
ON CONFLICT ("SwimmerId","SessionDate") DO NOTHING;

COMMIT;

-- Verify:
--   SELECT count(*) FROM attendance.attendance_record;
--   SELECT s."Code", count(*) FROM attendance.attendance_record r
--     JOIN reference.attendance_status s ON s."Id" = r."StatusId" GROUP BY s."Code" ORDER BY 1;
