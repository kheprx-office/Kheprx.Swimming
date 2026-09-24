"""Throwaway runner: execute a .sql script against Postgres over SSL using pg8000.
Credentials come from env vars (PGHOST/PGPORT/PGDB/PGUSER/PGPW) so no secret is stored here.
Usage: python _run_sql.py <path-to-sql>
"""
import os
import ssl
import sys
import pg8000

sql_path = sys.argv[1]
with open(sql_path, "r", encoding="utf-8") as f:
    raw = f.read()

# Strip line comments (cut each line at the first '--'), then split on ';'.
lines = []
for line in raw.splitlines():
    idx = line.find("--")
    if idx != -1:
        line = line[:idx]
    lines.append(line)
stmts = [s.strip() for s in "\n".join(lines).split(";") if s.strip()]

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

conn = pg8000.connect(
    user=os.environ["PGUSER"],
    password=os.environ["PGPW"],
    host=os.environ["PGHOST"],
    port=int(os.environ["PGPORT"]),
    database=os.environ["PGDB"],
    ssl_context=ctx,
)
cur = conn.cursor()
applied = 0
for s in stmts:
    if s.upper() in ("BEGIN", "COMMIT", "START TRANSACTION"):
        continue
    cur.execute(s)
    applied += 1
conn.commit()
print(f"executed {applied} statement(s)")

cur.execute(
    """
    SELECT rs."Id"::text AS session, count(rr.*) AS results
    FROM championships.race_session rs
    JOIN championships.competition_day d ON d."Id" = rs."DayId"
    LEFT JOIN championships.race_result rr ON rr."RaceSessionId" = rs."Id"
    WHERE d."EventId" = '33333333-3333-3333-3333-333333333301'
    GROUP BY rs."Id" ORDER BY rs."Id"
    """
)
print("VERIFY (race_session -> result count):")
for row in cur.fetchall():
    print(f"  {row[0]}  ->  {row[1]}")

cur.execute(
    """
    SELECT rr."SwimmerId"::text, rr."TimeMs", rr."Points", rr."IsPersonalBest"
    FROM championships.race_result rr
    WHERE rr."RaceSessionId" = '66666666-6666-6666-6666-666666660001'
    ORDER BY rr."TimeMs"
    """
)
print("SEEDED 50m Free rows (swimmer -> timeMs, points, pb):")
for row in cur.fetchall():
    print(f"  {row[0]}  ->  {row[1]}ms  pts={row[2]}  pb={row[3]}")

conn.close()
