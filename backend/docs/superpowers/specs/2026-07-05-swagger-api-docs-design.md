# Swagger API Documentation — Design

**Date:** 2026-07-05
**Status:** Approved
**Scope:** Backend (`Kheprx.BaseBackend.Api`) — full-contract Swagger/OpenAPI documentation for all 16 endpoints across 5 controllers.

## Goal

Turn the existing bare-bones Swagger setup (`AddSwaggerGen()` with zero configuration) into a full reference contract: JWT-authorized interactive UI, per-endpoint summaries, documented `ApiResponse<T>` envelope shapes for every status code, and machine-readable error codes. The Swagger UI remains Development-only.

## Decisions

| Decision | Choice |
|---|---|
| Deliverable | Rich interactive Swagger UI (no committed spec file, no static HTML) |
| Exposure | Development environment only (unchanged from current behavior) |
| Detail level | Full contract: summaries, all status codes, envelope types, error codes |
| Mechanism | Standard attributes + XML doc comments; one operation filter for auth responses |
| New packages | None — `Swashbuckle.AspNetCore` is already referenced |

## Architecture

### 1. Swagger infrastructure (one-time setup)

**`Kheprx.BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs`** — replace the bare `AddSwaggerGen()` with a configured call:

- **Doc metadata:** `SwaggerDoc("v1", ...)` with title "Kheprx Electric API", version `v1`, and a short platform description.
- **JWT Bearer security scheme:** `AddSecurityDefinition` with `Type = Http`, `Scheme = "bearer"`, `BearerFormat = "JWT"`. The UI shows an **Authorize** button; paste an access token from `POST /api/auth/login` once and all protected endpoints become callable.
- **`AuthorizeOperationFilter`** registered via `OperationFilter<>`.
- **XML comments:** `IncludeXmlComments` for three assemblies — `Kheprx.BaseBackend.Api` (controller/action docs), `Kheprx.BaseBackend.Identity.Application` and `Kheprx.BaseBackend.Catalog.Application` (DTO/request property descriptions).

**`AuthorizeOperationFilter`** (new file `Kheprx.BaseBackend.Api/Swagger/AuthorizeOperationFilter.cs`, ~40 lines):

- For every operation whose action or controller carries `[Authorize]` and not `[AllowAnonymous]`:
  - attach the Bearer `OpenApiSecurityRequirement`;
  - add a documented `401 Unauthorized` response (if the action hasn't already documented one explicitly);
  - if the `[Authorize]` attribute restricts roles (e.g. `Roles = "admin"` on `UsersController`), also add `403 Forbidden`.
- This is the single convention in the design: it derives 401/403 from the *real* auth attributes, so documentation can never drift from enforcement, and saves hand-writing those responses on the 9 protected endpoints.

**csproj changes** (3 files): `Kheprx.BaseBackend.Api`, `Kheprx.BaseBackend.Identity.Application`, `Kheprx.BaseBackend.Catalog.Application` each get:

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<NoWarn>$(NoWarn);1591</NoWarn>
```

`NoWarn 1591` suppresses "missing XML comment" warnings for the many public members we intentionally leave undocumented.

**Unchanged:** `ApplicationBuilderExtensions.cs` keeps `UseSwagger()`/`UseSwaggerUI()` inside the `IsDevelopment()` guard. No middleware, routing, or behavior changes anywhere.

### 2. Per-endpoint contract annotations

Every action gets:

1. a `///` XML `<summary>` (one line, imperative: "Authenticates a user and issues a token pair.");
2. `<response code="...">` tags naming the machine-readable error code(s) for that status;
3. `[ProducesResponseType(typeof(ApiResponse<T>), StatusCodes.Status...)]` per outcome, using the **concrete envelope type** so consumers see `successStatus`/`message`/`error`/`data`.

Rules:

- **Validation 400s:** every body-accepting action (8 total) documents `400` as `ApiResponse<object>` — the shape produced by `ValidationFilter`/`ModelStateResponse` (message = localized "validation failed", error = joined messages).
- **Auth 401/403:** owned by `AuthorizeOperationFilter`, **not** repeated in attributes — except where 401 carries a business body with a specific error code (login, refresh, me, change-password), which document it explicitly; the filter skips adding its generic 401 when one is already present.
- **Route casing:** routes come from `[Route("api/[controller]")]` and are matched case-insensitively; documentation displays them as generated.

#### Endpoint matrix

**AuthController** (`api/auth`)

| Endpoint | Auth | Responses |
|---|---|---|
| `POST /login` | anonymous | 200 `ApiResponse<SessionDto>` · 400 validation · 401 `INVALID_CREDENTIALS` |
| `POST /refresh` | anonymous | 200 `ApiResponse<SessionDto>` · 400 validation · 401 `INVALID_REFRESH_TOKEN` |
| `POST /logout` | bearer | 200 `ApiResponse<object>` · 401 (filter) |
| `GET /me` | bearer | 200 `ApiResponse<CurrentUserDto>` · 401 `NOT_AUTHENTICATED` |
| `POST /change-password` | bearer | 200 `ApiResponse<SessionDto>` · 400 validation · 401 `INVALID_CREDENTIALS` |

**UsersController** (`api/users`, controller-level `[Authorize(Roles = "admin")]` → filter adds 401 + 403 to all)

| Endpoint | Responses |
|---|---|
| `GET ?search=` | 200 `ApiResponse<IReadOnlyList<UserDto>>` |
| `POST` | 201 `ApiResponse<UserDto>` · 400 validation · 409 `EMAIL_IN_USE` \| `NID_IN_USE` |
| `PATCH /{id}` | 200 `ApiResponse<UserDto>` · 400 validation · 404 `USER_NOT_FOUND` · 409 `EMAIL_IN_USE` \| `NID_IN_USE` |
| `PATCH /{id}/status` | 200 `ApiResponse<UserDto>` · 400 validation · 404 `USER_NOT_FOUND` |

**RolesController** (`api/roles`)

| Endpoint | Auth | Responses |
|---|---|---|
| `GET` | bearer | 200 `ApiResponse<IReadOnlyList<RoleDto>>` · 401 (filter) |

**EngagementTypesController** (`api/engagement-types`)

| Endpoint | Auth | Responses |
|---|---|---|
| `GET` | bearer | 200 `ApiResponse<IReadOnlyList<EngagementTypeDto>>` · 401 (filter) |

**ProductsController** (`api/products`, **anonymous** — see observation below)

| Endpoint | Responses |
|---|---|
| `GET` | 200 `ApiResponse<IReadOnlyList<ProductDto>>` |
| `GET /{id}` | 200 `ApiResponse<ProductDto>` · 404 `PRODUCT_NOT_FOUND` |
| `POST` | 201 `ApiResponse<ProductDto>` · 400 validation |
| `PUT /{id}` | 200 `ApiResponse<ProductDto>` · 400 validation · 404 `PRODUCT_NOT_FOUND` |
| `DELETE /{id}` | 200 `ApiResponse<object>` · 404 `PRODUCT_NOT_FOUND` |

> **Observation (out of scope):** `ProductsController` carries no `[Authorize]` attribute — all five product endpoints, including create/update/delete, are open to anonymous callers. The documentation reflects this faithfully. Whether that is intended is an auth-policy question, not a documentation one; flagged here for a separate decision.

### 3. Error handling, testing, verification

- **New unit tests** in the existing `Kheprx.BaseBackend.Api.UnitTests` project for `AuthorizeOperationFilter`:
  - `[Authorize]` action → security requirement attached + 401 documented;
  - `[Authorize(Roles = ...)]` → 403 also documented;
  - `[AllowAnonymous]` / unattributed → nothing added;
  - explicit 401 already present → filter does not overwrite it.
- **Existing tests untouched:** `[ProducesResponseType]` and XML comments are metadata-only; the full existing suite must pass unchanged.
- **Build:** `dotnet build` completes clean with XML doc generation enabled.
- **Manual verification:** run the API with `ASPNETCORE_ENVIRONMENT=Development`, open `/swagger`, confirm all 16 endpoints grouped by controller with summaries and response tables, click Authorize with a real token, and successfully call one protected endpoint (e.g. `GET /api/auth/me`) from the UI.

## Out of scope

- Committed/exported OpenAPI spec file (can be added later with `Swashbuckle.AspNetCore.Cli` if the frontend wants a build-time contract).
- Serving Swagger outside Development.
- Any change to `ProductsController` authorization.
- API versioning (single `v1` doc).
