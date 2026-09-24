-- seed-distances-aiven.sql
-- Seeds reference.distance with the canonical race distances (fixed ids). Idempotent.
-- NOTE: EF column names are PascalCase -> must be double-quoted in Postgres.

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

INSERT INTO reference.distance ("Id","Code","NameEn","NameAr","Meters") VALUES
  ('44444444-4444-4444-4444-444444000001', '50m',    '50m',    '٥٠ متر',     50),
  ('44444444-4444-4444-4444-444444000002', '100m',   '100m',   '١٠٠ متر',    100),
  ('44444444-4444-4444-4444-444444000003', '200m',   '200m',   '٢٠٠ متر',    200),
  ('44444444-4444-4444-4444-444444000004', '400m',   '400m',   '٤٠٠ متر',    400),
  ('44444444-4444-4444-4444-444444000005', '800m',   '800m',   '٨٠٠ متر',    800),
  ('44444444-4444-4444-4444-444444000006', '1000m',  '1000m',  '١٠٠٠ متر',   1000),
  ('44444444-4444-4444-4444-444444000007', '1500m',  '1500m',  '١٥٠٠ متر',   1500),
  ('44444444-4444-4444-4444-444444000008', '5000m',  '5000m',  '٥٠٠٠ متر',   5000),
  ('44444444-4444-4444-4444-444444000009', '7000m',  '7000m',  '٧٠٠٠ متر',   7000),
  ('44444444-4444-4444-4444-444444000010', '7500m',  '7500m',  '٧٥٠٠ متر',   7500),
  ('44444444-4444-4444-4444-444444000011', '10000m', '10000m', '١٠٠٠٠ متر', 10000)
ON CONFLICT ("Code") DO NOTHING;

COMMIT;

-- Verify:
--   SELECT "Code","Meters" FROM reference.distance ORDER BY "Meters";
