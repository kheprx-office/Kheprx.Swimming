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
