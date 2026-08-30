# Swagger API Documentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the bare `AddSwaggerGen()` into a full-contract Swagger doc for all 16 endpoints: JWT-authorized UI, per-endpoint summaries, `ApiResponse<T>` envelope shapes per status code, and machine-readable error codes.

**Architecture:** One-time SwaggerGen configuration (doc metadata, Bearer scheme, XML comments) plus a single `AuthorizeOperationFilter` that derives 401/403 documentation from the real `[Authorize]` attributes. Every controller action then gets XML summaries, `<response>` tags naming error codes, and `[ProducesResponseType]` attributes with concrete envelope types. DTO records get `<param>` docs that Swashbuckle surfaces as schema property descriptions.

**Tech Stack:** ASP.NET Core (.NET 10), Swashbuckle.AspNetCore 7.2.0 (already referenced), xunit 2.9.2 + Moq 4.20.72 for tests.

**Spec:** `backend/docs/superpowers/specs/2026-07-05-swagger-api-docs-design.md`

## Global Constraints

- No new NuGet packages. Swashbuckle.AspNetCore 7.2.0 is already pinned in `backend/Directory.Packages.props` (central package management — never put `Version=` in a csproj).
- Swagger UI stays Development-only: do NOT touch `ApplicationBuilderExtensions.cs`.
- No behavior changes anywhere — attributes and XML comments are metadata-only; the existing test suite must pass unchanged.
- Security scheme id is exactly `"Bearer"` — the filter (Task 1) and `AddSecurityDefinition` (Task 2) must both use this string.
- Envelope types in `[ProducesResponseType]` are always the concrete `ApiResponse<T>` (from `Kheprx.BaseBackend.SharedKernel.Responses`), never the bare DTO.
- Validation 400s are typed `ApiResponse<object>` (the `ModelStateResponse.From` shape).
- Filter-owned 401/403: protected endpoints do NOT repeat 401/403 in attributes or `<response>` tags — exception: login, refresh, me, change-password, whose 401 carries a business error code and is documented explicitly.
- All commands below run from the repo root `C:\Users\envnt\Desktop\Kheprx.Electric`.
- Work happens on the current branch `feat/identity-auth`.
- Commit message style: `feat(api): ...` / `docs(api): ...` / `test(api): ...` with the `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` trailer.

---

### Task 1: AuthorizeOperationFilter (TDD)

**Files:**
- Create: `backend/Kheprx.BaseBackend.Api/Swagger/AuthorizeOperationFilter.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/AuthorizeOperationFilterTests.cs`

**Interfaces:**
- Consumes: `Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter` (transitively available to the test project via the Api project reference — no csproj changes needed).
- Produces: `public sealed class AuthorizeOperationFilter : IOperationFilter` in namespace `Kheprx.BaseBackend.Api.Swagger`, registered by Task 2 via `options.OperationFilter<AuthorizeOperationFilter>()`. It references security scheme id `"Bearer"`, which Task 2 defines.

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Kheprx.BaseBackend.Api.UnitTests/AuthorizeOperationFilterTests.cs`:

```csharp
using System.Text.Json;
using Kheprx.BaseBackend.Api.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class AuthorizeOperationFilterTests
{
    [Authorize]
    private sealed class ProtectedController
    {
        public void Action() { }
        [AllowAnonymous] public void Anon() { }
    }

    [Authorize(Roles = "admin")]
    private sealed class AdminController
    {
        public void Action() { }
    }

    private sealed class OpenController
    {
        public void Plain() { }
        [Authorize] public void Locked() { }
    }

    private static OpenApiOperation Apply(Type controllerType, string methodName, OpenApiOperation? operation = null)
    {
        operation ??= new OpenApiOperation();
        var context = new OperationFilterContext(
            new ApiDescription(),
            new SchemaGenerator(new SchemaGeneratorOptions(),
                new JsonSerializerDataContractResolver(new JsonSerializerOptions())),
            new SchemaRepository(),
            controllerType.GetMethod(methodName)!);
        new AuthorizeOperationFilter().Apply(operation, context);
        return operation;
    }

    [Fact]
    public void Controller_level_authorize_adds_security_requirement_and_401()
    {
        var operation = Apply(typeof(ProtectedController), nameof(ProtectedController.Action));

        Assert.Single(operation.Security);
        Assert.True(operation.Responses.ContainsKey("401"));
        Assert.False(operation.Responses.ContainsKey("403"));
    }

    [Fact]
    public void Role_restricted_authorize_adds_403()
    {
        var operation = Apply(typeof(AdminController), nameof(AdminController.Action));

        Assert.True(operation.Responses.ContainsKey("401"));
        Assert.True(operation.Responses.ContainsKey("403"));
    }

    [Fact]
    public void Action_level_authorize_adds_security_requirement_and_401()
    {
        var operation = Apply(typeof(OpenController), nameof(OpenController.Locked));

        Assert.Single(operation.Security);
        Assert.True(operation.Responses.ContainsKey("401"));
    }

    [Fact]
    public void Unattributed_action_is_untouched()
    {
        var operation = Apply(typeof(OpenController), nameof(OpenController.Plain));

        Assert.Empty(operation.Security);
        Assert.Empty(operation.Responses);
    }

    [Fact]
    public void Allow_anonymous_action_on_protected_controller_is_untouched()
    {
        var operation = Apply(typeof(ProtectedController), nameof(ProtectedController.Anon));

        Assert.Empty(operation.Security);
        Assert.Empty(operation.Responses);
    }

    [Fact]
    public void Existing_401_response_is_not_overwritten()
    {
        var operation = new OpenApiOperation();
        operation.Responses["401"] = new OpenApiResponse { Description = "custom" };

        Apply(typeof(ProtectedController), nameof(ProtectedController.Action), operation);

        Assert.Equal("custom", operation.Responses["401"].Description);
        Assert.Single(operation.Security); // security requirement is still attached
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter AuthorizeOperationFilterTests`
Expected: build FAILS with `CS0246: The type or namespace name 'AuthorizeOperationFilter' could not be found` (and the missing `Kheprx.BaseBackend.Api.Swagger` namespace).

- [ ] **Step 3: Write the implementation**

Create `backend/Kheprx.BaseBackend.Api/Swagger/AuthorizeOperationFilter.cs`:

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kheprx.BaseBackend.Api.Swagger;

/// <summary>
/// Derives 401/403 documentation and the Bearer security requirement from the real
/// [Authorize] attributes, so the doc can never drift from enforcement.
/// </summary>
public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            return;

        var authorizeAttributes = context.MethodInfo.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(context.MethodInfo.DeclaringType?.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                    ?? Enumerable.Empty<AuthorizeAttribute>())
            .ToArray();

        if (authorizeAttributes.Length == 0)
            return;

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            }] = Array.Empty<string>()
        });

        operation.Responses.TryAdd("401", new OpenApiResponse
        {
            Description = "Missing or invalid access token."
        });

        if (authorizeAttributes.Any(a => !string.IsNullOrWhiteSpace(a.Roles)))
            operation.Responses.TryAdd("403", new OpenApiResponse
            {
                Description = "Authenticated but lacking the required role."
            });
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter AuthorizeOperationFilterTests`
Expected: PASS — 6 tests.

- [ ] **Step 5: Commit**

```powershell
git add backend/Kheprx.BaseBackend.Api/Swagger/AuthorizeOperationFilter.cs backend/tests/Kheprx.BaseBackend.Api.UnitTests/AuthorizeOperationFilterTests.cs
git commit -m @'
feat(api): AuthorizeOperationFilter documents 401/403 from [Authorize]

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 2: SwaggerGen configuration + XML doc generation

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs` (replace line 30, `services.AddSwaggerGen();`)
- Modify: `backend/Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Kheprx.BaseBackend.Identity.Application.csproj`
- Modify: `backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Application/Kheprx.BaseBackend.Catalog.Application.csproj`

**Interfaces:**
- Consumes: `AuthorizeOperationFilter` from Task 1 (namespace `Kheprx.BaseBackend.Api.Swagger`).
- Produces: security scheme `"Bearer"`; XML docs generated for the Api + both Application assemblies, ingested via `IncludeXmlComments`. Tasks 3–7's XML comments only surface in Swagger because of this task.

- [ ] **Step 1: Enable XML doc generation in the three csproj files**

In `backend/Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj`, extend the existing `<PropertyGroup>`:

```xml
<PropertyGroup>
  <UserSecretsId>61e495bb-e5db-41e3-9fc2-870575400b6e</UserSecretsId>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

In `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Kheprx.BaseBackend.Identity.Application.csproj` and `backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Application/Kheprx.BaseBackend.Catalog.Application.csproj` (neither currently has a `<PropertyGroup>`), add as the first child of `<Project>`:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

- [ ] **Step 2: Configure SwaggerGen**

In `backend/Kheprx.BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs`, add two usings at the top:

```csharp
using Kheprx.BaseBackend.Api.Swagger;
using Microsoft.OpenApi.Models;
```

Replace `services.AddSwaggerGen();` with:

```csharp
services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Kheprx Electric API",
        Version = "v1",
        Description = "Backend API for the Kheprx Electric platform. Every endpoint wraps its payload in the "
            + "ApiResponse envelope: { successStatus, message, error, data }. Failure responses carry a "
            + "machine-readable code in 'error' (e.g. EMAIL_IN_USE)."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the accessToken returned by POST /api/auth/login."
    });

    options.OperationFilter<AuthorizeOperationFilter>();

    var documentedAssemblies = new[]
    {
        typeof(ServiceCollectionExtensions).Assembly,
        typeof(Kheprx.BaseBackend.Identity.Application.DTOs.SessionDto).Assembly,
        typeof(Kheprx.BaseBackend.Catalog.Application.DTOs.ProductDto).Assembly,
    };
    foreach (var assembly in documentedAssemblies)
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
        if (File.Exists(xmlPath))
            options.IncludeXmlComments(xmlPath);
    }
});
```

- [ ] **Step 3: Build and run the full test suite**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, 0 warnings (NoWarn 1591 suppresses missing-comment warnings).

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: PASS — all existing tests plus the 6 from Task 1.

- [ ] **Step 4: Commit**

```powershell
git add backend/Kheprx.BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs backend/Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Kheprx.BaseBackend.Identity.Application.csproj backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Application/Kheprx.BaseBackend.Catalog.Application.csproj
git commit -m @'
feat(api): configure SwaggerGen - metadata, Bearer scheme, XML comments

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 3: AuthController annotations

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs` (annotations only — do not change any statement inside method bodies)

**Interfaces:**
- Consumes: `ApiResponse<T>` envelope, `StatusCodes` constants (already in scope via implicit usings).
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Annotate all five actions**

Replace the action signatures/attributes so the file reads (method bodies unchanged from current code):

```csharp
using System.IdentityModel.Tokens.Jwt;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

public sealed class AuthController : BaseApiController
{
    private readonly IAuthService _service;

    public AuthController(IAuthService service) => _service = service;

    /// <summary>Authenticates a user with email and password and issues a token pair.</summary>
    /// <response code="200">Signed in; returns the session.</response>
    /// <response code="400">Validation failed; joined messages in the envelope's error field.</response>
    /// <response code="401">Wrong email or password — error code INVALID_CREDENTIALS.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SessionDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<SessionDto>>> Login(LoginRequest request, CancellationToken ct)
    {
        // ... body unchanged ...
    }

    /// <summary>Exchanges a refresh token for a new access/refresh token pair.</summary>
    /// <response code="200">New session issued; the old refresh token is invalidated.</response>
    /// <response code="400">Validation failed; joined messages in the envelope's error field.</response>
    /// <response code="401">Refresh token unknown, expired, or revoked — error code INVALID_REFRESH_TOKEN.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SessionDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<SessionDto>>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        // ... body unchanged ...
    }

    /// <summary>Signs the current user out and revokes their refresh token.</summary>
    /// <response code="200">Signed out; data is null.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Logout(CancellationToken ct) // AD-009: no body
    {
        // ... body unchanged ...
    }

    /// <summary>Returns the profile of the currently authenticated user.</summary>
    /// <response code="200">The current user.</response>
    /// <response code="401">Token valid but user no longer resolvable — error code NOT_AUTHENTICATED.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CurrentUserDto>>> Me(CancellationToken ct)
    {
        // ... body unchanged ...
    }

    /// <summary>Changes the current user's password and issues a fresh session.</summary>
    /// <response code="200">Password changed; returns a new session.</response>
    /// <response code="400">Validation failed; joined messages in the envelope's error field.</response>
    /// <response code="401">Current password incorrect — error code INVALID_CREDENTIALS.</response>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SessionDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<SessionDto>>> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        // ... body unchanged ...
    }

    private Guid CurrentUserId()
        => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : Guid.Empty;
}
```

(`// ... body unchanged ...` above means: keep the existing method bodies exactly as they are in the current file — only the XML comments and `[ProducesResponseType]` attributes are new.)

- [ ] **Step 2: Build and run the Api unit tests**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests`
Expected: PASS — annotations are metadata-only; `AuthControllerTests` unchanged and green.

- [ ] **Step 3: Commit**

```powershell
git add backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs
git commit -m @'
docs(api): swagger contract annotations for AuthController

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 4: UsersController annotations

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/UsersController.cs` (annotations only — method bodies unchanged)

**Interfaces:**
- Consumes: nothing new. Controller-level `[Authorize(Roles = "admin")]` stays; the Task 1 filter documents 401 + 403 on all four actions — do not add 401/403 here.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Annotate all four actions**

```csharp
    /// <summary>Lists users, optionally filtered by a search term (name, email, code, or NID).</summary>
    /// <response code="200">The matching users.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserDto>>>> List(
        [FromQuery] string? search, CancellationToken ct)
```

```csharp
    /// <summary>Creates a user with a role-dependent profile.</summary>
    /// <response code="201">User created.</response>
    /// <response code="400">Validation failed; joined messages in the envelope's error field.</response>
    /// <response code="409">Email or NID already taken — error code EMAIL_IN_USE or NID_IN_USE.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create(CreateUserRequest request, CancellationToken ct)
```

```csharp
    /// <summary>Updates a user's details, role, status, and profile.</summary>
    /// <response code="200">User updated.</response>
    /// <response code="400">Validation failed; joined messages in the envelope's error field.</response>
    /// <response code="404">No user with this id — error code USER_NOT_FOUND.</response>
    /// <response code="409">Email or NID already taken — error code EMAIL_IN_USE or NID_IN_USE.</response>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(Guid id, UpdateUserRequest request, CancellationToken ct)
```

```csharp
    /// <summary>Activates or deactivates a user.</summary>
    /// <response code="200">Status updated.</response>
    /// <response code="400">Validation failed; joined messages in the envelope's error field.</response>
    /// <response code="404">No user with this id — error code USER_NOT_FOUND.</response>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserDto>>> SetStatus(Guid id, SetUserStatusRequest request, CancellationToken ct)
```

- [ ] **Step 2: Build and run the Api unit tests**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests`
Expected: PASS — `UsersControllerTests` unchanged and green.

- [ ] **Step 3: Commit**

```powershell
git add backend/Kheprx.BaseBackend.Api/Controllers/UsersController.cs
git commit -m @'
docs(api): swagger contract annotations for UsersController

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 5: RolesController + EngagementTypesController annotations

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/RolesController.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/EngagementTypesController.cs`

**Interfaces:**
- Consumes: nothing new. Both actions carry `[Authorize]` (no roles) — the Task 1 filter documents their 401; do not add it here.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Annotate RolesController.Get**

```csharp
    /// <summary>Lists all selectable roles, ordered for display.</summary>
    /// <response code="200">All roles.</response>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RoleDto>>>> Get(CancellationToken ct)
```

- [ ] **Step 2: Annotate EngagementTypesController.Get**

```csharp
    /// <summary>Lists all engagement types, ordered for display.</summary>
    /// <response code="200">All engagement types.</response>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EngagementTypeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EngagementTypeDto>>>> Get(CancellationToken ct)
```

- [ ] **Step 3: Build and run the Api unit tests**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests`
Expected: PASS.

- [ ] **Step 4: Commit**

```powershell
git add backend/Kheprx.BaseBackend.Api/Controllers/RolesController.cs backend/Kheprx.BaseBackend.Api/Controllers/EngagementTypesController.cs
git commit -m @'
docs(api): swagger contract annotations for Roles and EngagementTypes

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 6: ProductsController annotations

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ProductsController.cs` (annotations only — method bodies unchanged)

**Interfaces:**
- Consumes: nothing new. NOTE: this controller has no `[Authorize]` — all five endpoints are anonymous. Document faithfully; do NOT add auth attributes (out of scope per spec).
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Annotate all five actions**

```csharp
    /// <summary>Lists all products.</summary>
    /// <response code="200">All products.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProductDto>>>> GetAll(CancellationToken ct)
```

```csharp
    /// <summary>Returns a single product by id.</summary>
    /// <response code="200">The product.</response>
    /// <response code="404">No product with this id — error code PRODUCT_NOT_FOUND.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetById(int id, CancellationToken ct)
```

```csharp
    /// <summary>Creates a product.</summary>
    /// <response code="201">Product created; Location header points at GET /api/products/{id}.</response>
    /// <response code="400">Validation failed; joined messages in the envelope's error field.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Create(
        CreateProductRequest request, CancellationToken ct)
```

```csharp
    /// <summary>Replaces a product's name, price, and description.</summary>
    /// <response code="200">Product updated.</response>
    /// <response code="400">Validation failed; joined messages in the envelope's error field.</response>
    /// <response code="404">No product with this id — error code PRODUCT_NOT_FOUND.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Update(
        int id, UpdateProductRequest request, CancellationToken ct)
```

```csharp
    /// <summary>Deletes a product.</summary>
    /// <response code="200">Product deleted; data is null.</response>
    /// <response code="404">No product with this id — error code PRODUCT_NOT_FOUND.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken ct)
```

- [ ] **Step 2: Build and run the full test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: PASS — all projects.

- [ ] **Step 3: Commit**

```powershell
git add backend/Kheprx.BaseBackend.Api/Controllers/ProductsController.cs
git commit -m @'
docs(api): swagger contract annotations for ProductsController

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 7: DTO schema descriptions

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/AuthDtos.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/UserDtos.cs`
- Modify: `backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Application/DTOs/ProductDtos.cs`

**Interfaces:**
- Consumes: XML doc generation enabled in Task 2. Swashbuckle 7.x maps record `<param>` docs to schema property descriptions.
- Produces: nothing consumed by later tasks. Record declarations (names, parameter lists) must not change — comments only.

- [ ] **Step 1: Document AuthDtos.cs**

```csharp
namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>Credentials for POST /api/auth/login.</summary>
/// <param name="Email">Account email address.</param>
/// <param name="Password">Account password.</param>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Token exchange payload for POST /api/auth/refresh.</summary>
/// <param name="RefreshToken">The refresh token issued with the last session.</param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Payload for POST /api/auth/change-password.</summary>
/// <param name="CurrentPassword">The password being replaced.</param>
/// <param name="NewPassword">The new password to set.</param>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>An authenticated session: token pair plus signed-in user essentials.</summary>
/// <param name="AccessToken">JWT for the Authorization: Bearer header.</param>
/// <param name="RefreshToken">Token used to obtain a new pair via POST /api/auth/refresh.</param>
/// <param name="Role">Role code of the signed-in user (e.g. admin).</param>
/// <param name="UserId">Identifier of the signed-in user.</param>
/// <param name="MustChangePassword">True when the user must change their password before continuing.</param>
public sealed record SessionDto(string AccessToken, string RefreshToken, string Role, Guid UserId, bool MustChangePassword);

/// <summary>Profile of the currently authenticated user (GET /api/auth/me).</summary>
/// <param name="UserId">User identifier.</param>
/// <param name="Email">Account email address.</param>
/// <param name="FullName">Display name.</param>
/// <param name="Role">Role code (e.g. admin).</param>
public sealed record CurrentUserDto(Guid UserId, string Email, string FullName, string Role);

/// <summary>A selectable role.</summary>
/// <param name="Id">Role identifier.</param>
/// <param name="Code">Stable role code (e.g. admin).</param>
/// <param name="LabelAr">Arabic display label.</param>
/// <param name="LabelEn">English display label.</param>
/// <param name="SortOrder">Display ordering, ascending.</param>
public sealed record RoleDto(Guid Id, string Code, string? LabelAr, string? LabelEn, int SortOrder);
```

- [ ] **Step 2: Document UserDtos.cs**

```csharp
namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>Role-dependent employment profile attached to a user.</summary>
/// <param name="MonthlySalary">Monthly salary, for salaried roles.</param>
/// <param name="EngagementTypeId">Engagement type identifier, for engaged roles.</param>
/// <param name="EngagementTypeLabel">Resolved engagement type label for display.</param>
/// <param name="DailyWage">Daily wage, for waged roles.</param>
/// <param name="HireDate">Date the user was hired.</param>
public sealed record UserProfileDto(
    decimal? MonthlySalary, Guid? EngagementTypeId, string? EngagementTypeLabel,
    decimal? DailyWage, DateOnly? HireDate);

/// <summary>A managed user account.</summary>
/// <param name="Id">User identifier.</param>
/// <param name="Code">Human-readable user code.</param>
/// <param name="FullName">Display name.</param>
/// <param name="Email">Account email address; null when the user signs in by NID only.</param>
/// <param name="Phone">Phone number.</param>
/// <param name="Gender">Gender code.</param>
/// <param name="Age">Age in years.</param>
/// <param name="Nid">National ID (unique).</param>
/// <param name="Role">Role code (e.g. admin).</param>
/// <param name="Status">Account status (active/inactive).</param>
/// <param name="MustChangePassword">True when the user must change their password at next sign-in.</param>
/// <param name="Profile">Role-dependent employment profile; null when the role has none.</param>
public sealed record UserDto(
    Guid Id, string? Code, string FullName, string? Email, string? Phone,
    string? Gender, int? Age, string Nid, string Role, string Status,
    bool MustChangePassword, UserProfileDto? Profile);

/// <summary>Payload for POST /api/users.</summary>
/// <param name="FullName">Display name.</param>
/// <param name="Role">Role code; determines which profile fields apply.</param>
/// <param name="Nid">National ID (must be unique).</param>
/// <param name="Email">Email address; optional for NID-only accounts.</param>
/// <param name="Password">Initial password; server may require a change at first sign-in.</param>
/// <param name="Phone">Phone number.</param>
/// <param name="Gender">Gender code.</param>
/// <param name="Age">Age in years.</param>
/// <param name="MonthlySalary">Monthly salary, for salaried roles.</param>
/// <param name="EngagementTypeId">Engagement type identifier, for engaged roles.</param>
/// <param name="DailyWage">Daily wage, for waged roles.</param>
/// <param name="HireDate">Date the user was hired.</param>
public sealed record CreateUserRequest(
    string FullName, string Role, string Nid,
    string? Email, string? Password, string? Phone, string? Gender, int? Age,
    decimal? MonthlySalary, Guid? EngagementTypeId, decimal? DailyWage, DateOnly? HireDate);

/// <summary>Payload for PATCH /api/users/{id}.</summary>
/// <param name="FullName">Display name.</param>
/// <param name="Role">Role code; determines which profile fields apply.</param>
/// <param name="Nid">National ID (must be unique).</param>
/// <param name="Status">Account status (active/inactive).</param>
/// <param name="Email">Email address; optional for NID-only accounts.</param>
/// <param name="Password">New password; omit to keep the current one.</param>
/// <param name="Phone">Phone number.</param>
/// <param name="Gender">Gender code.</param>
/// <param name="Age">Age in years.</param>
/// <param name="MonthlySalary">Monthly salary, for salaried roles.</param>
/// <param name="EngagementTypeId">Engagement type identifier, for engaged roles.</param>
/// <param name="DailyWage">Daily wage, for waged roles.</param>
/// <param name="HireDate">Date the user was hired.</param>
public sealed record UpdateUserRequest(
    string FullName, string Role, string Nid, string Status,
    string? Email, string? Password, string? Phone, string? Gender, int? Age,
    decimal? MonthlySalary, Guid? EngagementTypeId, decimal? DailyWage, DateOnly? HireDate);

/// <summary>Payload for PATCH /api/users/{id}/status.</summary>
/// <param name="Status">Target account status (active/inactive).</param>
public sealed record SetUserStatusRequest(string Status);

/// <summary>A selectable engagement type.</summary>
/// <param name="Id">Engagement type identifier.</param>
/// <param name="Code">Stable engagement type code.</param>
/// <param name="LabelAr">Arabic display label.</param>
/// <param name="LabelEn">English display label.</param>
/// <param name="SortOrder">Display ordering, ascending.</param>
public sealed record EngagementTypeDto(Guid Id, string Code, string? LabelAr, string? LabelEn, int SortOrder);
```

- [ ] **Step 3: Document ProductDtos.cs**

```csharp
namespace Kheprx.BaseBackend.Catalog.Application.DTOs;

/// <summary>A catalog product.</summary>
/// <param name="Id">Product identifier.</param>
/// <param name="Name">Product name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Price">Unit price.</param>
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
/// <param name="UpdatedAt">Last update timestamp (UTC); null if never updated.</param>
public sealed record ProductDto(
    int Id, string Name, string? Description, decimal Price, DateTime CreatedAt, DateTime? UpdatedAt);

/// <summary>Payload for POST /api/products.</summary>
/// <param name="Name">Product name.</param>
/// <param name="Price">Unit price.</param>
/// <param name="Description">Optional description.</param>
public sealed record CreateProductRequest(string Name, decimal Price, string? Description);

/// <summary>Payload for PUT /api/products/{id}.</summary>
/// <param name="Name">Product name.</param>
/// <param name="Price">Unit price.</param>
/// <param name="Description">Optional description.</param>
public sealed record UpdateProductRequest(string Name, decimal Price, string? Description);
```

- [ ] **Step 4: Build and run the full test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/AuthDtos.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/UserDtos.cs backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Application/DTOs/ProductDtos.cs
git commit -m @'
docs(api): schema descriptions for Identity and Catalog DTOs

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 8: End-to-end verification of the generated document

**Files:** none created or modified — verification only.

**Interfaces:**
- Consumes: everything above.

> **DB note:** `Program.cs` runs EF migrations at startup against the configured Aiven database — this is the project's established dev workflow and is idempotent. This task only reads `/swagger`; it fires no data-mutating requests.

- [ ] **Step 1: Run the API in Development**

Run (background): `dotnet run --project backend/Kheprx.BaseBackend.Api`
Expected: Serilog startup logs, listening on `https://localhost:7080` and `http://localhost:5080` (launchSettings sets `ASPNETCORE_ENVIRONMENT=Development`).

- [ ] **Step 2: Assert the document structure**

Run:

```powershell
$doc = Invoke-RestMethod http://localhost:5080/swagger/v1/swagger.json
"title=" + $doc.info.title
"operations=" + ($doc.paths.PSObject.Properties | ForEach-Object { $_.Value.PSObject.Properties.Name } | Measure-Object).Count
"bearer=" + ($null -ne $doc.components.securitySchemes.Bearer)
"login401=" + ($null -ne $doc.paths.'/api/Auth/login'.post.responses.'401')
"users403=" + ($null -ne $doc.paths.'/api/Users'.get.responses.'403')
"products-anon=" + ($null -eq $doc.paths.'/api/Products'.get.security)
```

Expected output:

```
title=Kheprx Electric API
operations=16
bearer=True
login401=True
users403=True
products-anon=True
```

(If path casing differs — e.g. `/api/auth/login` — adjust the property names to match `$doc.paths.PSObject.Properties.Name`; the operation count and flags are what matter.)

- [ ] **Step 3: Manual UI spot-check**

Open `http://localhost:5080/swagger` in a browser and confirm:
1. All 5 controller groups appear with 16 operations, each with a summary line.
2. Protected endpoints show a padlock; the **Authorize** button accepts a token from `POST /api/auth/login` (use a provisioned account), after which `GET /api/auth/me` returns 200 from the UI.
3. `POST /api/users` shows 201/400/401/403/409 with `ApiResponse` schemas, and the 409 description names EMAIL_IN_USE / NID_IN_USE.
4. Schemas section shows property descriptions for `UserDto`, `SessionDto`, `ProductDto`.

- [ ] **Step 4: Stop the API and run the full suite one last time**

Stop the background `dotnet run` process, then:

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: PASS — all projects green. Working tree clean (`git status`) since Task 8 changes nothing.
