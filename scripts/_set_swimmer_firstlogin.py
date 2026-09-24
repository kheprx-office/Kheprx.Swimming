"""Throwaway: re-arm a swimmer's first-login flag so the onboarding wizard shows again.

Credentials come from env vars (PGHOST/PGPORT/PGDB/PGUSER/PGPW) — no secret stored here.
Targets identity.app_user (columns are quoted PascalCase). Prints the row before/after.

Usage (PowerShell), from repo root:
  $env:PGHOST='kheprx-service-kheprx.b.aivencloud.com'; $env:PGPORT='14647';
  $env:PGDB='Swimming_Production'; $env:PGUSER='avnadmin'; $env:PGPW='<AIVEN_PASSWORD>';
  python scripts/_set_swimmer_firstlogin.py
"""
import os
import ssl
import pg8000

USERNAME = "swimmer01"

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

cur.execute('SELECT "Username", "Email", "IsFirstLogin" FROM identity.app_user WHERE "Username" = %s', (USERNAME,))
before = cur.fetchone()
print("BEFORE:", before)
if before is None:
    print(f"!! No app_user with Username='{USERNAME}' in database '{os.environ['PGDB']}'. Nothing changed.")
    conn.close()
    raise SystemExit(1)

cur.execute('UPDATE identity.app_user SET "IsFirstLogin" = TRUE WHERE "Username" = %s', (USERNAME,))
conn.commit()

cur.execute('SELECT "Username", "Email", "IsFirstLogin" FROM identity.app_user WHERE "Username" = %s', (USERNAME,))
print("AFTER: ", cur.fetchone())
print(f"\nDone. Log in with:  email = swimmer01@kheprx.local   password = Passw0rd!   role = Swimmer  -> onboarding wizard.")
conn.close()
