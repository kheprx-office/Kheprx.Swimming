# Starter Guide — Adding a Feature

This scaffold ships infrastructure + authentication only (backend `Identity` module; frontend
`auth`, `user-management`, `home`). Each feature is added independently. Use the retained `Identity`
module (backend) and `auth` feature (frontend) as copy-me templates.

## Backend — new module `<Name>`
1. Copy `backend/src/Modules/Identity` to `backend/src/Modules/<Name>`, renaming the four projects
   and their namespaces `Identity` -> `<Name>` (Domain / Application / Infrastructure / Contracts).
   Keep the `Kheprx.BaseBackend.` prefix.
2. Add the projects to the solution: `dotnet sln backend/Kheprx.BaseBackend.sln add <the new csproj paths>`.
3. In `backend/Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj`, add a `<ProjectReference>` to
   `<Name>.Infrastructure`.
4. In `backend/Kheprx.BaseBackend.Api/Program.cs`, call
   `builder.Services.Add<Name>Module(builder.Configuration);` and, after build,
   `await app.Apply<Name>MigrationsAsync();`.
5. In `backend/Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs`, add
   `Apply<Name>MigrationsAsync` mirroring the Identity one.
6. Add a `<Name>Controller` under `backend/Kheprx.BaseBackend.Api/Controllers/` (inherit
   `BaseApiController`).
7. Verify: `dotnet build backend/Kheprx.BaseBackend.sln` && `dotnet test backend/Kheprx.BaseBackend.sln`.

## Frontend — new feature `<name>`
1. Copy `frontend/src/app/features/auth` to `frontend/src/app/features/<name>`
   (`presentation/` + `domain/` + `data/` (+ `testing/`)), and update its `index.ts` barrel exports.
2. The API layer is hand-written: add request/response DTOs under `data/dto/`, a domain repository
   interface under `domain/repositories/`, and its implementation under `data/repositories/`, using
   `core/network/api/http-client.ts`. (There is no generated client to regenerate.)
3. Add a route in `frontend/src/app/app.routes.ts`.
4. Add a nav entry to the `allItems` array in `frontend/src/app/layout/layout.component.ts`.
5. Verify: `cd frontend && npm run build`.

## Contracts
`contracts/generated/frontend-api-client` is an empty placeholder reserved for a future generated
client. No generator is wired today; the frontend API layer is hand-written per feature.

## Configuration
`backend/Kheprx.BaseBackend.Api/appsettings.json` has a localhost Postgres placeholder. Point it at
your own database; the host applies Identity EF Core migrations on startup, so a reachable Postgres
is required to run the API.

## Verify the whole scaffold
- Backend: `dotnet build backend/Kheprx.BaseBackend.sln` and `dotnet test backend/Kheprx.BaseBackend.sln`
- Frontend: `cd frontend && npm run build`

## Swimming — auth + coach/captain profile (run against Aiven)

The `Identity` module targets the swimming schema (`identity.app_user` + `head_coach_profile`/
`captain_profile`, `reference.role`/`gender`). Login is email + password; `is_first_login` forces a
password change on first sign-in. The frontend is bilingual EN/AR (live toggle + LTR↔RTL) with a
light/dark theme.

### Point the API at `Swimming_Production`
Use `dotnet user-secrets` (never commit the password):
```
dotnet user-secrets init --project backend/Kheprx.BaseBackend.Api
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=kheprx-service-kheprx.b.aivencloud.com;Port=14647;Database=Swimming_Production;Username=avnadmin;Password=<AIVEN_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true" --project backend/Kheprx.BaseBackend.Api
dotnet user-secrets set "Jwt:SigningKey" "<random-48+char-string>" --project backend/Kheprx.BaseBackend.Api
```
Then `dotnet ef database update --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure --startup-project backend/Kheprx.BaseBackend.Api --context IdentityDbContext`, and run the API (`dotnet run --project backend/Kheprx.BaseBackend.Api`) — startup applies the migration and runs the idempotent seeder.

### Seeded demo logins (dev credentials — not production secrets)
- `headcoach@kheprx.local` / `Passw0rd!` — role `head_coach`, `is_first_login = true` (forced change-password on first login)
- `captain@kheprx.local` / `Passw0rd!` — role `captain`, `is_first_login = false`

### Frontend end-to-end checklist (with the API running against `Swimming_Production`)
1. `cd frontend && npm start`; open the app → login is English LTR, teal/cream, no demo box.
2. Log in as the captain → lands on `/home`; the shell shows **Settings** enabled, other nav disabled.
3. Open Settings → profile shows name/email/role/phone/age/national_id; toggle **language** → the whole
   shell + Settings flip to Arabic RTL live; toggle **theme** → dark palette; both persist across reload.
4. Change the password inline → success message.
5. Log out; log in as the head-coach (`is_first_login`) → forced to `/change-password`; navigating to
   `/account` bounces back until the password is changed; after changing → reaches Settings (role badge "Head Coach").

### Notes
- The API layer is hand-written (see **Contracts** above) — the auth DTOs live under
  `frontend/src/app/features/auth/data/dto/`; there is nothing to regenerate.
- Production `ng build` inlines Google Fonts, which needs network access at build time. For fully
  offline builds, self-host the fonts or set `optimization.fonts.inline: false` in `angular.json`.
  `ng build --configuration development` and `npm test` do not need network.
