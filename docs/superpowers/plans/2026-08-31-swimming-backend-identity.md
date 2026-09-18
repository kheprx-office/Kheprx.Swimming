# Swimming Backend — Identity Reshape + Auth/Profile API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reshape the still-Electric-shaped `Identity` module to the swimming schema (`app_user`, `role`, `gender`, `head_coach_profile`, `captain_profile`), create it in the empty `Swimming_Production` database via one fresh EF migration, seed a head-coach and a captain user, and expose real email+password login plus an enriched profile endpoint.

**Architecture:** .NET modular monolith, Clean Architecture per module (Domain → Application → Infrastructure → Api host). Reshape flows layer-by-layer so each layer's project builds and its Moq/xUnit tests pass before the next. The two coach-side profiles are structurally identical; the profile endpoint joins whichever subtype the user's role maps to. Refresh-token rotation is kept as auth plumbing.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql 10, PBKDF2 password hashing, JWT (HMAC-SHA256), xUnit 2.9 + Moq 4.20.

**Spec:** `docs/superpowers/specs/2026-08-31-swimming-auth-profile-design.md`

## Global Constraints

- Target framework `net10.0`; EF Core / Npgsql `10.0.0`; xUnit `2.9.2`; Moq `4.20.72` (all pinned in `backend/Directory.Packages.props` — do not add package versions inline).
- Roles for this slice: **`head_coach`** and **`captain`** only (no `admin`/`swimmer`).
- `coach` == `captain` (captain_profile). Head coach has broader access than captain (role claim only for now).
- Schemas: default `identity`; `role` and `gender` live in schema `reference`. `refresh_token` stays in `identity`.
- Real Aiven connection string + JWT signing key come from `dotnet user-secrets` / env vars — **never committed**. Target DB name is exactly **`Swimming_Production`**, `SSL Mode=Require`.
- Namespaces/project names stay `Kheprx.BaseBackend.*` (do not rename).
- Keep the existing region-comment style and record-DTO conventions already in the module.
- Do **not** run `git commit` until the user explicitly says so. (Steps below include `git add`/`commit` — stage and prepare the message, but hold the actual commit until told.)

---

### Task 1: Domain reshape — entities

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/AppUser.cs`
- Create: `.../Identity.Domain/Entities/Gender.cs`
- Create: `.../Identity.Domain/Entities/HeadCoachProfile.cs`
- Create: `.../Identity.Domain/Entities/CaptainProfile.cs`
- Modify: `.../Identity.Domain/Entities/Role.cs`
- Delete: `.../Identity.Domain/Entities/{User,Manager,Moqawel,Worker}.cs`
- Delete: `.../Identity.Domain/Repositories/{IManagerReadRepository,IMoqawelReadRepository,IWorkerReadRepository,IUserProfileRepository}.cs`
- Delete: `.../Identity.Domain/ReadModels/{ManagerNameRow,MoqawelListRow,WorkerDetailRow,WorkerListRow}.cs`
- Modify: `.../Identity.Domain/Repositories/IUserRepository.cs` (retarget to `AppUser`)
- Modify: `.../Identity.Domain/Repositories/IRoleRepository.cs` (drop `LabelEn/Ar` assumptions if any — verify signatures use `Role`, not labels)
- Create: `.../Identity.Domain/Repositories/ICoachProfileRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/AppUserTests.cs`
- Modify/Delete: `.../Identity.UnitTests/Entities/UserTests.cs` → replace with `AppUserTests.cs`; `.../Entities/ProfileEntityTests.cs` (retarget to coach profiles or delete Electric cases)

**Interfaces:**
- Produces:
  - `AppUser(string username, string nameEn, Guid roleId, string? nameAr = null, string? email = null, string? passwordHash = null, Guid? genderId = null, DateOnly? dob = null, string? phone = null, bool isFirstLogin = true)`; readonly props `Id, Username, Email, PasswordHash, NameEn, NameAr, GenderId, Dob, Phone, RoleId, IsFirstLogin, CreatedAt`; computed `int? Age`; methods `void SetPassword(string hash)` (clears `IsFirstLogin`), `void UpdateProfile(string nameEn, string? nameAr, string? email, Guid? genderId, DateOnly? dob, string? phone)`.
  - `Role(string code, string nameEn, string? nameAr = null)`; props `Id, Code, NameEn, NameAr`.
  - `Gender(string code, string nameEn, string? nameAr = null)`; props `Id, Code, NameEn, NameAr`.
  - `HeadCoachProfile(Guid userId, string nationalId)` and `CaptainProfile(Guid userId, string nationalId)`; props `Id, UserId, NationalId, CreatedAt`.
  - `ICoachProfileRepository` (defined here, implemented in Task 3): `Task<string?> GetNationalIdAsync(Guid userId, string roleCode, CancellationToken ct = default)`, `Task AddHeadCoachAsync(HeadCoachProfile p, CancellationToken ct = default)`, `Task AddCaptainAsync(CaptainProfile p, CancellationToken ct = default)`, `Task SaveChangesAsync(CancellationToken ct = default)`.
  - `IUserRepository` (unchanged method names, `User` → `AppUser`): `GetByEmailAsync`, `GetByIdAsync`, `ListAsync(string? search = null, …)`, `AddAsync(AppUser, …)`, `SaveChangesAsync`. **Remove** `GetByNidAsync` and `ListCodesByPrefixAsync` (no NID/code column on `app_user`).

- [ ] **Step 1: Write the failing test** — `AppUserTests.cs`

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Exceptions;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class AppUserTests
{
    private static AppUser New(bool firstLogin = true) =>
        new("captain.dave", "Dave Coach", Guid.NewGuid(),
            nameAr: "ديف", email: "Dave@Oasis.com", passwordHash: "hash", isFirstLogin: firstLogin);

    [Fact]
    public void Ctor_normalizes_email_and_trims_names()
    {
        var u = New();
        Assert.Equal("dave@oasis.com", u.Email);
        Assert.Equal("Dave Coach", u.NameEn);
        Assert.True(u.IsFirstLogin);
    }

    [Fact]
    public void SetPassword_clears_first_login()
    {
        var u = New(firstLogin: true);
        u.SetPassword("newhash");
        Assert.Equal("newhash", u.PasswordHash);
        Assert.False(u.IsFirstLogin);
    }

    [Fact]
    public void Ctor_requires_username_and_name_en()
    {
        Assert.Throws<InvalidUserException>(() => new AppUser("", "Dave", Guid.NewGuid()));
        Assert.Throws<InvalidUserException>(() => new AppUser("dave", " ", Guid.NewGuid()));
    }

    [Fact]
    public void Age_is_null_when_no_dob_and_computed_when_present()
    {
        Assert.Null(New().Age);
        var born = new AppUser("u", "n", Guid.NewGuid(), dob: new DateOnly(2000, 1, 1));
        Assert.True(born.Age >= 25);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~AppUserTests`
Expected: FAIL to build — `AppUser` does not exist.

- [ ] **Step 3: Write the entities**

`AppUser.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Exceptions;

namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class AppUser
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? PasswordHash { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public Guid? GenderId { get; private set; }
    public DateOnly? Dob { get; private set; }
    public string? Phone { get; private set; }
    public Guid RoleId { get; private set; }
    public bool IsFirstLogin { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private AppUser() { } // EF Core

    public AppUser(
        string username, string nameEn, Guid roleId,
        string? nameAr = null, string? email = null, string? passwordHash = null,
        Guid? genderId = null, DateOnly? dob = null, string? phone = null, bool isFirstLogin = true)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new InvalidUserException("Username is required.");
        if (string.IsNullOrWhiteSpace(nameEn)) throw new InvalidUserException("English name is required.");

        Id = Guid.NewGuid();
        Username = username.Trim();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
        RoleId = roleId;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        PasswordHash = string.IsNullOrWhiteSpace(passwordHash) ? null : passwordHash;
        GenderId = genderId;
        Dob = dob;
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        IsFirstLogin = isFirstLogin;
        CreatedAt = DateTime.UtcNow;
    }

    public int? Age
    {
        get
        {
            if (Dob is null) return null;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - Dob.Value.Year;
            if (Dob.Value > today.AddYears(-age)) age--;
            return age;
        }
    }

    public void SetPassword(string hash)
    {
        PasswordHash = hash;
        IsFirstLogin = false;
    }

    public void UpdateProfile(
        string nameEn, string? nameAr, string? email, Guid? genderId, DateOnly? dob, string? phone)
    {
        if (string.IsNullOrWhiteSpace(nameEn)) throw new InvalidUserException("English name is required.");
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        GenderId = genderId;
        Dob = dob;
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
    }
}
```

`Gender.cs`:

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class Gender
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    private Gender() { } // EF Core

    public Gender(string code, string nameEn, string? nameAr = null)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        NameEn = nameEn.Trim();
        NameAr = nameAr;
    }
}
```

`Role.cs` (replace body):

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class Role
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    private Role() { } // EF Core

    public Role(string code, string nameEn, string? nameAr = null)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        NameEn = nameEn.Trim();
        NameAr = nameAr;
    }
}
```

`HeadCoachProfile.cs` (and an identical `CaptainProfile.cs`, swapping the class name):

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class HeadCoachProfile
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string NationalId { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private HeadCoachProfile() { } // EF Core

    public HeadCoachProfile(Guid userId, string nationalId)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        NationalId = nationalId.Trim();
        CreatedAt = DateTime.UtcNow;
    }
}
```

`ICoachProfileRepository.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;

namespace Kheprx.BaseBackend.Identity.Domain.Repositories;

public interface ICoachProfileRepository
{
    // Returns the national_id of the user's profile subtype (head_coach_profile or
    // captain_profile) selected by roleCode, or null when no matching profile row exists.
    Task<string?> GetNationalIdAsync(Guid userId, string roleCode, CancellationToken ct = default);
    Task AddHeadCoachAsync(HeadCoachProfile profile, CancellationToken ct = default);
    Task AddCaptainAsync(CaptainProfile profile, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

Then delete the Electric entity/repository/read-model files listed above, and retarget `IUserRepository` (`User` → `AppUser`, remove `GetByNidAsync` and `ListCodesByPrefixAsync`).

- [ ] **Step 4: Run the domain build + tests**

Run: `dotnet build backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain`
Then: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~Entities`
Expected: Domain project builds; entity tests PASS. (Application/Infrastructure/Api do not build yet — that is expected and fixed in Tasks 2–4.)

- [ ] **Step 5: Stage the change** (hold the commit until the user says so)

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities
# prepared message: "refactor(identity): reshape domain entities to swimming schema (AppUser/Gender/coach profiles)"
```

---

### Task 2: Application reshape — auth, profile enrichment, DTOs

**Files:**
- Modify: `.../Identity.Application/Abstractions/IJwtTokenService.cs` (`User` → `AppUser`)
- Modify: `.../Identity.Application/DTOs/AuthDtos.cs` (`CurrentUserDto` gains `NameEn/NameAr/NationalId`; `RoleDto` → `(Guid Id, string Code, string NameEn, string? NameAr)`)
- Modify: `.../Identity.Application/Services/AuthService.cs`
- Modify: `.../Identity.Application/Services/Interfaces/IAuthService.cs` (return-type names unchanged; verify)
- Modify: `.../Identity.Application/Validators/AuthRequestValidators.cs` (verify only Email/Password/Current/New — no change likely)
- Test: `.../Identity.UnitTests/Services/AuthServiceTests.cs` (rewrite for `AppUser` + profile mock)

**Interfaces:**
- Consumes (from Task 1): `AppUser`, `ICoachProfileRepository`, `IUserRepository` (AppUser).
- Produces:
  - `IJwtTokenService.CreateAccessToken(AppUser user, string roleCode)` (+ `CreateRefreshToken()`, `HashRefreshToken(string)` unchanged).
  - `CurrentUserDto(Guid UserId, string Email, string NameEn, string? NameAr, string Role, string? Phone, string? Gender, int? Age, string? NationalId)`.
  - `AuthService` constructor now also takes `ICoachProfileRepository profiles`: `AuthService(IUserRepository users, IRoleRepository roles, IRefreshTokenRepository tokens, ICoachProfileRepository profiles, IPasswordHasher hasher, IJwtTokenService jwt, IOptions<JwtOptions> options)`.
  - `SessionDto` unchanged shape; its `MustChangePassword` field is populated from `AppUser.IsFirstLogin`.

- [ ] **Step 1: Update `IJwtTokenService` + `AuthDtos`**

`IJwtTokenService.cs` — change the signature to `string CreateAccessToken(AppUser user, string roleCode);` and update the `using` to `Kheprx.BaseBackend.Identity.Domain.Entities`.

`AuthDtos.cs` — replace `CurrentUserDto` and `RoleDto`:

```csharp
/// <summary>Profile of the currently authenticated user (GET /api/auth/me).</summary>
public sealed record CurrentUserDto(
    Guid UserId, string Email, string NameEn, string? NameAr, string Role,
    string? Phone, string? Gender, int? Age, string? NationalId);

/// <summary>A selectable role.</summary>
public sealed record RoleDto(Guid Id, string Code, string NameEn, string? NameAr);
```

- [ ] **Step 2: Rewrite the failing `AuthServiceTests`**

```csharp
using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<IRefreshTokenRepository> _tokens = new();
    private readonly Mock<ICoachProfileRepository> _profiles = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IJwtTokenService> _jwt = new();
    private readonly Guid _roleId = Guid.NewGuid();

    private AuthService NewService() => new(
        _users.Object, _roles.Object, _tokens.Object, _profiles.Object,
        _hasher.Object, _jwt.Object, Options.Create(new JwtOptions { RefreshTokenDays = 7 }));

    private AppUser User(string pwHash = "stored", bool firstLogin = false)
        => new("captain.dave", "Dave", _roleId, email: "a@b.com", passwordHash: pwHash, isFirstLogin: firstLogin);

    [Fact]
    public async Task Login_unknown_email_returns_null()
    {
        _users.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CancellationToken>())).ReturnsAsync((AppUser?)null);
        Assert.Null(await NewService().LoginAsync(new LoginRequest("a@b.com", "pw")));
    }

    [Fact]
    public async Task Login_wrong_password_returns_null()
    {
        _users.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CancellationToken>())).ReturnsAsync(User());
        _hasher.Setup(h => h.Verify("pw", "stored")).Returns(false);
        Assert.Null(await NewService().LoginAsync(new LoginRequest("a@b.com", "pw")));
    }

    [Fact]
    public async Task Login_without_password_returns_null()
    {
        var u = new AppUser("u", "n", _roleId, email: "a@b.com"); // PasswordHash null
        _users.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CancellationToken>())).ReturnsAsync(u);
        Assert.Null(await NewService().LoginAsync(new LoginRequest("a@b.com", "pw")));
    }

    [Fact]
    public async Task Login_success_issues_session_with_first_login_flag()
    {
        var u = User(firstLogin: true);
        _users.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CancellationToken>())).ReturnsAsync(u);
        _hasher.Setup(h => h.Verify("pw", "stored")).Returns(true);
        _roles.Setup(r => r.GetByIdAsync(_roleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role("captain", "Captain"));
        _jwt.Setup(j => j.CreateAccessToken(u, "captain")).Returns("access");
        _jwt.Setup(j => j.CreateRefreshToken()).Returns(("raw", "hash"));

        var session = await NewService().LoginAsync(new LoginRequest("a@b.com", "pw"));

        Assert.NotNull(session);
        Assert.Equal("captain", session!.Role);
        Assert.True(session.MustChangePassword);
    }

    [Fact]
    public async Task Me_returns_profile_with_national_id()
    {
        var u = User();
        _users.Setup(r => r.GetByIdAsync(u.Id, It.IsAny<CancellationToken>())).ReturnsAsync(u);
        _roles.Setup(r => r.GetByIdAsync(_roleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role("head_coach", "Head Coach"));
        _profiles.Setup(p => p.GetNationalIdAsync(u.Id, "head_coach", It.IsAny<CancellationToken>())).ReturnsAsync("29001011234567");

        var me = await NewService().GetCurrentUserAsync(u.Id);

        Assert.Equal("29001011234567", me!.NationalId);
        Assert.Equal("head_coach", me.Role);
        Assert.Equal("Dave", me.NameEn);
    }

    [Fact]
    public async Task ChangePassword_wrong_current_returns_null()
    {
        var u = User();
        _users.Setup(r => r.GetByIdAsync(u.Id, It.IsAny<CancellationToken>())).ReturnsAsync(u);
        _hasher.Setup(h => h.Verify("bad", "stored")).Returns(false);
        Assert.Null(await NewService().ChangePasswordAsync(u.Id, new ChangePasswordRequest("bad", "new")));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~AuthServiceTests`
Expected: FAIL to build (AuthService ctor/shape mismatch).

- [ ] **Step 4: Rewrite `AuthService`**

Apply these edits to `AuthService.cs`:
1. Add field `private readonly ICoachProfileRepository _profiles;` and the ctor parameter (see Interfaces block); assign it.
2. In `LoginAsync`, drop the `!user.IsActive` check and the `user.RecordLogin(); await _users.SaveChangesAsync(ct);` lines. New guard:

```csharp
var user = await _users.GetByEmailAsync(request.Email, ct);
if (user is null || user.PasswordHash is null) return null;
if (!_hasher.Verify(request.Password, user.PasswordHash)) return null;
return await IssueSessionAsync(user, ct);
```

3. In `RefreshAsync`, drop the `|| !user.IsActive` clause (keep the null check).
4. Replace `GetCurrentUserAsync`:

```csharp
public async Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
{
    var user = await _users.GetByIdAsync(userId, ct);
    if (user is null) return null;
    var role = await _roles.GetByIdAsync(user.RoleId, ct);
    var roleCode = role?.Code ?? FallbackRole;
    var genderLabel = await _users.GetGenderCodeAsync(user.GenderId, ct); // see note
    var nationalId = await _profiles.GetNationalIdAsync(user.Id, roleCode, ct);
    return new CurrentUserDto(
        user.Id, user.Email ?? string.Empty, user.NameEn, user.NameAr, roleCode,
        user.Phone, genderLabel, user.Age, nationalId);
}
```

   Note: add `Task<string?> GetGenderCodeAsync(Guid? genderId, CancellationToken ct = default)` to `IUserRepository` (implemented in Task 3 as a `Genders` lookup). Alternatively inject `IGenderRepository`; keeping it on `IUserRepository` avoids a new type. Update the `AuthServiceTests.Me_...` test to `_users.Setup(r => r.GetGenderCodeAsync(...)).ReturnsAsync("male");` and assert `me.Gender == "male"` if you exercise it.
5. Change `IssueSessionAsync`/`IssueAccessAsync` parameter type `User` → `AppUser`. `SessionDto`'s last arg becomes `user.IsFirstLogin` (replacing `user.MustChangePassword`).
6. Update `using` and remove the `FallbackRole = "worker"` literal → `"captain"` (or keep any string; it is a defensive fallback only).

- [ ] **Step 5: Run the application build + tests**

Run: `dotnet build backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application`
Then: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~AuthServiceTests`
Expected: Application builds; AuthService tests PASS. (User-management/role services in Application are handled in Step 6 before this fully passes — do Step 6 first if the project fails to build on `UserService`/`RoleService`.)

- [ ] **Step 6: Adapt `RoleService` + user-management Application code to compile**

- `RoleDto` already updated. In `RoleService.cs` map `role => new RoleDto(role.Id, role.Code, role.NameEn, role.NameAr)` and remove any `SortOrder`/`IsActive`/label references. Update `RoleServiceTests.cs` accordingly (assert `NameEn`).
- Rewrite `UserDtos.cs` to the swimming shape:

```csharp
public sealed record UserDto(
    Guid Id, string Username, string NameEn, string? NameAr, string? Email,
    string? Phone, Guid? GenderId, DateOnly? Dob, string Role);

public sealed record CreateUserRequest(
    string Username, string NameEn, string Role,
    string? NameAr, string? Email, string? Password, string? Phone, Guid? GenderId, DateOnly? Dob);

public sealed record UpdateUserRequest(
    string NameEn, string Role, string? NameAr, string? Email,
    string? Password, string? Phone, Guid? GenderId, DateOnly? Dob);
```

  Delete `UserProfileDto` and `SetUserStatusRequest` (no employment profile, no `is_active`).
- Rewrite `UserService.cs` to: `ListAsync` (map `AppUser` + role code), `CreateAsync` (role lookup by code, optional email/password, `new AppUser(...)`, `AddAsync`, `SaveChangesAsync`), `UpdateAsync` (load, `user.UpdateProfile(...)`, optional `SetPassword`, save). **Remove** `SetStatusAsync`, `NextCodeAsync`, the `CodePrefixes` table, and all Manager/Moqawel/Worker/profile helpers. `IUserService` loses `SetStatusAsync`.
- Update `UserServiceTests.cs` and `UserRequestValidatorsTests.cs` to the new records (drop NID/status/salary assertions; assert name_en/role handling). Update `UserRequestValidators.cs` to validate `NameEn` (required) + `Role` (required) instead of `FullName`/`Nid`.

Run: `dotnet build backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application`
Then: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~Services|FullyQualifiedName~Validators`
Expected: Application builds; service + validator tests PASS.

- [ ] **Step 7: Stage the change** (hold the commit)

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application backend/tests/Kheprx.BaseBackend.Identity.UnitTests
# prepared message: "refactor(identity): reshape application layer (auth, profile, user-management) to swimming schema"
```

---

### Task 3: Infrastructure reshape — configs, DbContext, repositories, JWT

**Files:**
- Create: `.../Identity.Infrastructure/Configurations/{AppUserConfiguration,GenderConfiguration,HeadCoachProfileConfiguration,CaptainProfileConfiguration}.cs`
- Modify: `.../Identity.Infrastructure/Configurations/RoleConfiguration.cs`
- Delete: `.../Identity.Infrastructure/Configurations/{UserConfiguration,ManagerConfiguration,MoqawelConfiguration,WorkerConfiguration}.cs`
- Modify: `.../Identity.Infrastructure/Data/IdentityDbContext.cs`
- Modify: `.../Identity.Infrastructure/Repositories/UserRepository.cs` (User → AppUser; add `GetGenderCodeAsync`)
- Modify: `.../Identity.Infrastructure/Repositories/RoleRepository.cs` (Role shape; verify `GetByCodeAsync`/`GetAllAsync` compile)
- Create: `.../Identity.Infrastructure/Repositories/CoachProfileRepository.cs`
- Delete: `.../Identity.Infrastructure/Repositories/{UserProfileRepository,ManagerReadRepository,MoqawelReadRepository,WorkerReadRepository,SearchPatterns}.cs` (SearchPatterns only if no longer referenced)
- Modify: `.../Identity.Infrastructure/Security/JwtTokenService.cs` (User → AppUser)
- Modify: `.../Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs` (DI)
- Modify: `.../Identity.Infrastructure/Services/IdentityModuleApi.cs` (if it references removed types — verify)

**Interfaces:**
- Consumes: everything produced in Tasks 1–2.
- Produces: EF mappings creating tables `identity.app_user`, `identity.head_coach_profile`, `identity.captain_profile`, `identity.refresh_token`, `reference.role`, `reference.gender`; `CoachProfileRepository : ICoachProfileRepository`.

- [ ] **Step 1: Rewrite the DbContext**

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Data;

public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Gender> Genders => Set<Gender>();
    public DbSet<HeadCoachProfile> HeadCoachProfiles => Set<HeadCoachProfile>();
    public DbSet<CaptainProfile> CaptainProfiles => Set<CaptainProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Step 2: Write the configurations**

`AppUserConfiguration.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_user");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).HasMaxLength(100).IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(u => u.NameAr).HasMaxLength(200);
        builder.Property(u => u.Phone).HasMaxLength(40);
        builder.Property(u => u.RoleId).IsRequired();
        builder.Property(u => u.IsFirstLogin).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.HasOne<Role>().WithMany().HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Gender>().WithMany().HasForeignKey(u => u.GenderId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

`RoleConfiguration.cs` (schema `reference`):

```csharp
builder.ToTable("role", "reference");
builder.HasKey(r => r.Id);
builder.Property(r => r.Code).HasMaxLength(50).IsRequired();
builder.HasIndex(r => r.Code).IsUnique();
builder.Property(r => r.NameEn).HasMaxLength(100).IsRequired();
builder.Property(r => r.NameAr).HasMaxLength(100);
```

`GenderConfiguration.cs` — identical shape to Role but `ToTable("gender", "reference")`.

`HeadCoachProfileConfiguration.cs` (and identical `CaptainProfileConfiguration` with `ToTable("captain_profile")`):

```csharp
builder.ToTable("head_coach_profile");
builder.HasKey(p => p.Id);
builder.Property(p => p.UserId).IsRequired();
builder.HasIndex(p => p.UserId).IsUnique();
builder.Property(p => p.NationalId).HasMaxLength(14).IsRequired();
builder.HasIndex(p => p.NationalId).IsUnique();
builder.Property(p => p.CreatedAt).IsRequired();
builder.HasOne<AppUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
```

- [ ] **Step 3: Rewrite repositories + JWT service**

- `UserRepository.cs`: change `User` → `AppUser`, delete `GetByNidAsync`/`ListCodesByPrefixAsync`, change `ListAsync` search to `EF.Functions.ILike(u.NameEn, pattern)` (drop the NID branch and the `SearchPatterns` dependency — inline `"%"+search.Trim()+"%"`), order by `u.NameEn`. Add:

```csharp
public Task<string?> GetGenderCodeAsync(Guid? genderId, CancellationToken ct = default)
    => genderId is null
        ? Task.FromResult<string?>(null)
        : _db.Genders.AsNoTracking().Where(g => g.Id == genderId).Select(g => (string?)g.Code).FirstOrDefaultAsync(ct);
```

- `CoachProfileRepository.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class CoachProfileRepository : ICoachProfileRepository
{
    private readonly IdentityDbContext _db;
    public CoachProfileRepository(IdentityDbContext db) => _db = db;

    public Task<string?> GetNationalIdAsync(Guid userId, string roleCode, CancellationToken ct = default) => roleCode switch
    {
        "head_coach" => _db.HeadCoachProfiles.AsNoTracking()
            .Where(p => p.UserId == userId).Select(p => (string?)p.NationalId).FirstOrDefaultAsync(ct),
        "captain" => _db.CaptainProfiles.AsNoTracking()
            .Where(p => p.UserId == userId).Select(p => (string?)p.NationalId).FirstOrDefaultAsync(ct),
        _ => Task.FromResult<string?>(null),
    };

    public async Task AddHeadCoachAsync(HeadCoachProfile profile, CancellationToken ct = default)
        => await _db.HeadCoachProfiles.AddAsync(profile, ct);

    public async Task AddCaptainAsync(CaptainProfile profile, CancellationToken ct = default)
        => await _db.CaptainProfiles.AddAsync(profile, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
```

- `JwtTokenService.cs`: change `CreateAccessToken(User user, …)` → `CreateAccessToken(AppUser user, …)` (body unchanged; `user.Email`/`user.Id` still exist).
- `IdentityModuleExtensions.cs`: remove the `IWorkerReadRepository`/`IMoqawelReadRepository`/`IManagerReadRepository`/`IUserProfileRepository` registrations; add `services.AddScoped<ICoachProfileRepository, CoachProfileRepository>();`.

- [ ] **Step 4: Build the Infrastructure project**

Run: `dotnet build backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure`
Expected: builds, 0 errors. (No unit tests here; DB behavior is verified in Task 7.)

- [ ] **Step 5: Stage the change** (hold the commit)

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure
# prepared message: "refactor(identity): reshape infrastructure (mappings, repos, jwt) to swimming schema"
```

---

### Task 4: API host — controllers, contracts cleanup, full solution green

**Files:**
- Modify: `.../Identity.Contracts/` — delete `ManagerNameDto.cs`, `MoqawelPersonDto.cs`, `PersonOptionDto.cs`, `WorkerDetailPersonDto.cs`, `WorkerPersonDto.cs`; verify `IIdentityModule.cs` no longer references them (trim its members if needed) and update `IdentityModuleApi.cs`.
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/UsersController.cs` (remove the status endpoint + `SetUserStatusRequest`; align to new DTOs)
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/RolesController.cs` (align to new `RoleDto`)
- Verify unchanged: `AuthController.cs` (its DTO shapes are stable).
- Modify: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/*` and `backend/tests/Kheprx.BaseBackend.ArchitectureTests/*` as needed to compile.

- [ ] **Step 1: Trim contracts + controllers**

Delete the five Electric person DTOs. In `IIdentityModule`/`IdentityModuleApi`, remove members returning those types (e.g. worker-detail lookups) — keep only members the Api still calls; if none remain, reduce `IIdentityModule` to an empty marker or delete it and its DI registration. In `UsersController`, delete the `PATCH {id}/status` action and its `SetUserStatusRequest` usage; keep list/create/update mapped to the new `UserDto`/`CreateUserRequest`/`UpdateUserRequest`. In `RolesController`, map to `RoleDto(Id, Code, NameEn, NameAr)`.

- [ ] **Step 2: Build the whole solution**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: FAIL first with concrete references to removed types; fix each until it builds 0 errors.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all projects PASS (Identity.UnitTests, Api.UnitTests, ArchitectureTests). Fix any architecture-test rule that references removed namespaces.

- [ ] **Step 4: Stage the change** (hold the commit)

```bash
git add backend
# prepared message: "refactor(identity): trim contracts + controllers; solution builds green on swimming schema"
```

---

### Task 5: Fresh EF migration `InitialSwimmingIdentity`

**Files:**
- Delete: `.../Identity.Infrastructure/Migrations/*` (all 5 Electric migrations + `IdentityDbContextModelSnapshot.cs`)
- Create: a new migration under `.../Identity.Infrastructure/Migrations/` via the EF CLI.

- [ ] **Step 1: Remove old migrations**

Delete every file under `.../Identity.Infrastructure/Migrations/`. (The DB is empty; there is no applied history to reconcile.)

- [ ] **Step 2: Generate the migration**

Run (uses the design-time `IdentityDbContextFactory`):
```bash
dotnet ef migrations add InitialSwimmingIdentity \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api \
  --context IdentityDbContext
```
Expected: a new `*_InitialSwimmingIdentity.cs` that creates schemas `identity` + `reference` and tables `role`, `gender`, `app_user`, `head_coach_profile`, `captain_profile`, `refresh_token`.

- [ ] **Step 3: Review the generated migration**

Open the generated file and confirm: both schemas created; `app_user` FKs to `reference.role` (Restrict) and `reference.gender` (Restrict); profile tables FK to `identity.app_user` (Cascade) with unique `user_id` + `national_id`; unique indexes on `app_user.username`/`email`. No leftover Electric columns (`nid`, `code`, `is_active`, salary/wage).

- [ ] **Step 4: Build (compile the generated Designer)**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: builds, 0 errors.

- [ ] **Step 5: Stage the change** (hold the commit)

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Migrations
# prepared message: "feat(identity): fresh InitialSwimmingIdentity migration (identity + reference schemas)"
```

---

### Task 6: Startup seeder — roles, genders, two demo users

**Files:**
- Create: `.../Identity.Infrastructure/Data/IdentitySeeder.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs` (call the seeder after migrating)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Data/IdentitySeederTests.cs` (in-memory/SQLite-less: assert idempotency via a `DbContext` with the InMemory provider **or**, simpler, unit-test the seeder's "insert-if-absent" decision on an `IdentityDbContext` backed by `UseInMemoryDatabase`).

**Interfaces:**
- Produces: `static Task IdentitySeeder.SeedAsync(IdentityDbContext db, IPasswordHasher hasher, CancellationToken ct = default)` — idempotent; inserts the two roles, two genders, and two users (+ matching profile rows) only when absent.

- [ ] **Step 1: Write the seeder**

```csharp
using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Data;

public static class IdentitySeeder
{
    // Dev credentials (documented in docs/STARTER.md — not production secrets).
    public const string DevPassword = "Passw0rd!";

    public static async Task SeedAsync(IdentityDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        var headCoachRole = await EnsureRole(db, "head_coach", "Head Coach", "المدرب العام", ct);
        var captainRole = await EnsureRole(db, "captain", "Captain", "الكابتن", ct);
        var male = await EnsureGender(db, "male", "Male", "ذكر", ct);
        await EnsureGender(db, "female", "Female", "أنثى", ct);
        await db.SaveChangesAsync(ct);

        await EnsureHeadCoach(db, hasher, headCoachRole.Id, male.Id, ct);
        await EnsureCaptain(db, hasher, captainRole.Id, male.Id, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Role> EnsureRole(IdentityDbContext db, string code, string en, string ar, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (role is null) { role = new Role(code, en, ar); await db.Roles.AddAsync(role, ct); }
        return role;
    }

    private static async Task<Gender> EnsureGender(IdentityDbContext db, string code, string en, string ar, CancellationToken ct)
    {
        var gender = await db.Genders.FirstOrDefaultAsync(g => g.Code == code, ct);
        if (gender is null) { gender = new Gender(code, en, ar); await db.Genders.AddAsync(gender, ct); }
        return gender;
    }

    private static async Task EnsureHeadCoach(IdentityDbContext db, IPasswordHasher hasher, Guid roleId, Guid genderId, CancellationToken ct)
    {
        const string email = "headcoach@kheprx.local";
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) return;
        var user = new AppUser("head.coach", "Head Coach", roleId, nameAr: "المدرب العام",
            email: email, passwordHash: hasher.Hash(DevPassword), genderId: genderId,
            dob: new DateOnly(1985, 3, 12), phone: "+201000000001", isFirstLogin: true);
        await db.Users.AddAsync(user, ct);
        await db.HeadCoachProfiles.AddAsync(new HeadCoachProfile(user.Id, "28503121234567"), ct);
    }

    private static async Task EnsureCaptain(IdentityDbContext db, IPasswordHasher hasher, Guid roleId, Guid genderId, CancellationToken ct)
    {
        const string email = "captain@kheprx.local";
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) return;
        var user = new AppUser("captain.dave", "Captain Dave", roleId, nameAr: "الكابتن ديف",
            email: email, passwordHash: hasher.Hash(DevPassword), genderId: genderId,
            dob: new DateOnly(1990, 7, 5), phone: "+201000000002", isFirstLogin: false);
        await db.Users.AddAsync(user, ct);
        await db.CaptainProfiles.AddAsync(new CaptainProfile(user.Id, "29007051234567"), ct);
    }
}
```

- [ ] **Step 2: Write the idempotency test**

```csharp
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Data;

public class IdentitySeederTests
{
    private static IdentityDbContext NewDb() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Seeding_twice_creates_each_row_once()
    {
        var hasher = new PasswordHasher();
        await using var db = NewDb();
        await IdentitySeeder.SeedAsync(db, hasher);
        await IdentitySeeder.SeedAsync(db, hasher);

        Assert.Equal(2, await db.Roles.CountAsync());
        Assert.Equal(2, await db.Genders.CountAsync());
        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Equal(1, await db.HeadCoachProfiles.CountAsync());
        Assert.Equal(1, await db.CaptainProfiles.CountAsync());
    }
}
```

Add the InMemory provider to the test project only: `Microsoft.EntityFrameworkCore.InMemory` (add `PackageVersion` `10.0.0` to `Directory.Packages.props` and a `PackageReference` in `Kheprx.BaseBackend.Identity.UnitTests.csproj`).

- [ ] **Step 3: Run the seeder test (fails, then passes)**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~IdentitySeederTests`
Expected: PASS after Step 1–2.

- [ ] **Step 4: Wire the seeder into startup**

In `MigrationExtensions.cs`, after `await db.Database.MigrateAsync();`:

```csharp
var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
await IdentitySeeder.SeedAsync(db, hasher);
```
(add the `using` for `IPasswordHasher` and `IdentitySeeder`.)

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: builds, 0 errors.

- [ ] **Step 5: Stage the change** (hold the commit)

```bash
git add backend
# prepared message: "feat(identity): idempotent startup seeder (roles, genders, head-coach + captain demo users)"
```

---

### Task 7: Connect to `Swimming_Production`, apply, verify end-to-end

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/appsettings.json` (keep localhost placeholder; document that real values come from secrets)
- Create/Modify: `docs/STARTER.md` (record dev credentials + user-secrets setup)

- [ ] **Step 1: Set the real connection via user-secrets** (do NOT commit these)

```bash
dotnet user-secrets init --project backend/Kheprx.BaseBackend.Api
dotnet user-secrets set "ConnectionStrings:Postgres" \
  "Host=kheprx-service-kheprx.b.aivencloud.com;Port=14647;Database=Swimming_Production;Username=avnadmin;Password=<AIVEN_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true" \
  --project backend/Kheprx.BaseBackend.Api
dotnet user-secrets set "Jwt:SigningKey" "<A_NEW_32B+_RANDOM_KEY>" --project backend/Kheprx.BaseBackend.Api
```

- [ ] **Step 2: Apply the migration to `Swimming_Production`**

Run: `dotnet ef database update --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure --startup-project backend/Kheprx.BaseBackend.Api --context IdentityDbContext`
Expected: schemas `identity` + `reference` and all six tables created in `Swimming_Production`. (If SSL/CA errors occur, confirm `Trust Server Certificate=true` or supply the Aiven CA cert path.)

- [ ] **Step 3: Run the API and verify the seeder + endpoints**

```bash
dotnet run --project backend/Kheprx.BaseBackend.Api
```
Then, in another shell:
```bash
# login as captain (is_first_login = false)
curl -s -X POST http://localhost:<port>/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"captain@kheprx.local","password":"Passw0rd!"}'
# expect 200, MustChangePassword=false, Role="captain"

# login as head coach (is_first_login = true) → MustChangePassword=true
curl -s -X POST http://localhost:<port>/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"headcoach@kheprx.local","password":"Passw0rd!"}'

# me (use the returned access token)
curl -s http://localhost:<port>/api/auth/me -H "Authorization: Bearer <ACCESS>"
# expect NameEn, Role, NationalId populated
```
Expected: captain logs in with `MustChangePassword=false`; head-coach with `true`; `/api/auth/me` returns the enriched profile incl. `NationalId`.

- [ ] **Step 4: Document dev credentials in `docs/STARTER.md`**

Add a "Swimming demo logins" section: `headcoach@kheprx.local / Passw0rd!` (forced first-login), `captain@kheprx.local / Passw0rd!`, plus the `dotnet user-secrets` commands (with the password redacted as `<AIVEN_PASSWORD>`).

- [ ] **Step 5: Stage the change** (hold the commit)

```bash
git add backend/Kheprx.BaseBackend.Api/appsettings.json docs/STARTER.md
# prepared message: "docs(identity): swimming dev logins + user-secrets connection setup"
```

## Self-Review Notes (author)

- **Spec coverage:** §4.1–4.5 → Tasks 1–4; §4.3 seed → Task 6; migration → Task 5; §4.5 secrets/DB → Task 7; §4.6 user-management adapt → Task 2 Step 6 + Task 4. Endpoints unchanged (AuthController) → verified in Task 4/7.
- **Deferred to Plans 2–3:** frontend i18n/theme, screens, contracts regen (§5–§7 of the spec).
- **Known adaptation:** "adapt-to-compile" for user-management became a genuine trim — `SetStatus`/NID/code/employment-profile have no swimming column and are removed (Task 2 Step 6, Task 4 Step 1). Surface this to the user if a fuller user-management is later wanted.
