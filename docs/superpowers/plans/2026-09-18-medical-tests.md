# Medical Tests Catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give a Head Coach an "add / list / delete" Medical Test catalog under the Captain Panel, backed by a new `Health` backend module owning the `health` schema.

**Architecture:** New `Health` backend module (Domain / Application / Contracts / Infrastructure) mirroring `Identity`, with its own `HealthDbContext` + migration pipeline; `medical_test.created_by` is a loose `Guid` (no cross-module FK). New Angular `features/medical-tests` data/domain slice consumed by a `captain-panel/medical-tests` page. Both `name_en` and `name_ar` required; head-coach only.

**Tech Stack:** .NET 10 (EF Core, Npgsql, FluentValidation, xUnit + Moq), Angular 20 (standalone, signals, Jest + TestBed).

**Spec:** `docs/superpowers/specs/2026-09-18-medical-tests-design.md`

## Global Constraints

- **Central package management:** `PackageReference` entries carry **no version** (versions live in `Directory.Packages.props`). Copy package ids exactly from the Identity projects.
- **Dev-server DLL lock:** stop the running API before any `dotnet build` / `dotnet ef` / `dotnet test` — the running server locks the build-output DLLs.
- **FluentValidation discovery:** validators are found by an AppDomain scan of assemblies whose name starts with `Kheprx.BaseBackend.`; `AddHealthModule(...)` MUST be called **before** `AddFluentValidationConfiguration()` in `Program.cs` so the Health.Application DLL is loaded when the scan runs.
- **Localization:** response messages are static classes keyed on `AppLanguage.Current` (`"ar"` → Arabic, else English), from `Kheprx.BaseBackend.SharedKernel.Resources`.
- **API base path:** `api/medical-tests` (override `BaseApiController`'s `[Route("api/[controller]")]`).
- **Authorization:** API `[Authorize(Roles = "head_coach")]`; route `roleGuard('head_coach')`; the Captain-Panel Medical Tests card is only shown to head coaches.
- **name_ar** column is `NOT NULL` (app rule: both names required), diverging from the diagram's nullable marking.
- **Angular conventions:** standalone components; signals (no Reactive Forms); Promise-based use cases returning `Result<T>`; repository ports are `InjectionToken`s, impls `providedIn: 'root'`, bound app-wide in `app.config.ts` via a `*_PROVIDERS` array.
- **Branch & commits:** work on a feature branch `feat/medical-tests` off `main`; do **not** push. Per the user's standing instruction, get explicit approval before the **first** commit; thereafter use each step's Commit checkpoint. Never commit to `main`.

---

## File Structure

**Backend — new (`backend/src/Modules/Health/`)**
- `Kheprx.BaseBackend.Health.Domain/` — `Entities/MedicalTest.cs`, `Repositories/IMedicalTestRepository.cs`, `.csproj`
- `Kheprx.BaseBackend.Health.Application/` — `DTOs/MedicalTestDtos.cs`, `Resources/MedicalTestMessages.cs`, `Validators/CreateMedicalTestRequestValidator.cs`, `Services/Interfaces/IMedicalTestService.cs`, `Services/MedicalTestService.cs`, `.csproj`
- `Kheprx.BaseBackend.Health.Contracts/` — `.csproj` (shell)
- `Kheprx.BaseBackend.Health.Infrastructure/` — `Data/HealthDbContext.cs`, `Data/HealthDbContextFactory.cs`, `Data/HealthSeeder.cs`, `Configurations/MedicalTestConfiguration.cs`, `Repositories/MedicalTestRepository.cs`, `Extensions/HealthModuleExtensions.cs`, `Migrations/*`, `.csproj`

**Backend — new tests:** `backend/tests/Kheprx.BaseBackend.Health.UnitTests/` (Entities, Repositories, Validators, Services, Data)

**Backend — edit:** `Kheprx.BaseBackend.Api/Controllers/MedicalTestsController.cs` (new), `Kheprx.BaseBackend.Api/Program.cs`, `Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs`, `Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj`, `Kheprx.BaseBackend.sln`

**Frontend — new (`frontend/src/app/features/medical-tests/`)**
- `data/dto/medical-test.dto.ts`, `data/repositories/medical-test.repository.impl.ts`, `data/medical-test.providers.ts`
- `domain/model/medical-test.ts`, `domain/repositories/medical-test.repository.ts`
- `domain/usecases/list-medical-tests.use-case.ts`, `create-medical-test.use-case.ts`, `delete-medical-test.use-case.ts`
- `index.ts`
- Page (under captain-panel): `features/captain-panel/presentation/pages/medical-tests/medical-tests.page.ts` + `.html` + `medical-tests.viewmodel.ts`
- `testing/` specs mirroring the above

**Frontend — edit:** `app.routes.ts`, `app.config.ts`, `features/captain-panel/index.ts`, `features/captain-panel/presentation/pages/captain-panel/captain-panel.page.ts` + `.html`, `features/captain-panel/testing/.../captain-panel.page.spec.ts`, `core/i18n/en.json`, `core/i18n/ar.json`

---

## Task B1: Health module skeleton (4 projects + test project, wired to the solution)

**Files:**
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Kheprx.BaseBackend.Health.Domain.csproj`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Kheprx.BaseBackend.Health.Application.csproj`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Contracts/Kheprx.BaseBackend.Health.Contracts.csproj`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Kheprx.BaseBackend.Health.Infrastructure.csproj`
- Create: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Kheprx.BaseBackend.Health.UnitTests.csproj`
- Modify: `backend/Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj` (add ProjectReference to Health.Infrastructure)
- Modify: `backend/Kheprx.BaseBackend.sln`

**Interfaces:**
- Produces: the four `Kheprx.BaseBackend.Health.*` projects + `Kheprx.BaseBackend.Health.UnitTests`, all building empty. Later tasks add types into them.

- [ ] **Step 1: Create the Domain csproj**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Kheprx.BaseBackend.Health.Domain.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\..\SharedKernel\Kheprx.BaseBackend.SharedKernel\Kheprx.BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the Application csproj**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Kheprx.BaseBackend.Health.Application.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="Microsoft.Extensions.Options" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Kheprx.BaseBackend.Health.Domain\Kheprx.BaseBackend.Health.Domain.csproj" />
    <ProjectReference Include="..\..\..\SharedKernel\Kheprx.BaseBackend.SharedKernel\Kheprx.BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Kheprx.BaseBackend.Health.Infrastructure" />
    <InternalsVisibleTo Include="Kheprx.BaseBackend.Health.UnitTests" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create the Contracts csproj** (shell)

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Contracts/Kheprx.BaseBackend.Health.Contracts.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

</Project>
```

- [ ] **Step 4: Create the Infrastructure csproj**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Kheprx.BaseBackend.Health.Infrastructure.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Kheprx.BaseBackend.Health.Domain\Kheprx.BaseBackend.Health.Domain.csproj" />
    <ProjectReference Include="..\Kheprx.BaseBackend.Health.Application\Kheprx.BaseBackend.Health.Application.csproj" />
    <ProjectReference Include="..\Kheprx.BaseBackend.Health.Contracts\Kheprx.BaseBackend.Health.Contracts.csproj" />
    <ProjectReference Include="..\..\..\SharedKernel\Kheprx.BaseBackend.SharedKernel\Kheprx.BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Kheprx.BaseBackend.Health.UnitTests" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Create the test csproj**

`backend/tests/Kheprx.BaseBackend.Health.UnitTests/Kheprx.BaseBackend.Health.UnitTests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Moq" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Modules\Health\Kheprx.BaseBackend.Health.Domain\Kheprx.BaseBackend.Health.Domain.csproj" />
    <ProjectReference Include="..\..\src\Modules\Health\Kheprx.BaseBackend.Health.Application\Kheprx.BaseBackend.Health.Application.csproj" />
    <ProjectReference Include="..\..\src\Modules\Health\Kheprx.BaseBackend.Health.Infrastructure\Kheprx.BaseBackend.Health.Infrastructure.csproj" />
    <ProjectReference Include="..\..\src\SharedKernel\Kheprx.BaseBackend.SharedKernel\Kheprx.BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Add the Health.Infrastructure reference to the Api project**

In `backend/Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj`, in the `<ItemGroup>` that holds the existing `Kheprx.BaseBackend.Identity.Infrastructure` ProjectReference, add alongside it:
```xml
    <ProjectReference Include="..\src\Modules\Health\Kheprx.BaseBackend.Health.Infrastructure\Kheprx.BaseBackend.Health.Infrastructure.csproj" />
```

- [ ] **Step 7: Add all five projects to the solution**

Run (from `backend/`):
```bash
dotnet sln Kheprx.BaseBackend.sln add \
  src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Kheprx.BaseBackend.Health.Domain.csproj \
  src/Modules/Health/Kheprx.BaseBackend.Health.Application/Kheprx.BaseBackend.Health.Application.csproj \
  src/Modules/Health/Kheprx.BaseBackend.Health.Contracts/Kheprx.BaseBackend.Health.Contracts.csproj \
  src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Kheprx.BaseBackend.Health.Infrastructure.csproj \
  tests/Kheprx.BaseBackend.Health.UnitTests/Kheprx.BaseBackend.Health.UnitTests.csproj
```

- [ ] **Step 8: Build the solution (verify the skeleton compiles)**

Run (from `backend/`, dev server stopped): `dotnet build Kheprx.BaseBackend.sln`
Expected: build succeeds; the five new projects compile (empty).

- [ ] **Step 9: Commit**

```bash
git add backend/src/Modules/Health backend/tests/Kheprx.BaseBackend.Health.UnitTests \
  backend/Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj backend/Kheprx.BaseBackend.sln
git commit -m "chore(health): scaffold Health module projects"
```

---

## Task B2: MedicalTest entity + repository port

**Files:**
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Entities/MedicalTest.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IMedicalTestRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/MedicalTestTests.cs`

**Interfaces:**
- Produces: `MedicalTest` (ctor `MedicalTest(string nameEn, string nameAr, string unit, decimal lowerBound, decimal upperBound, Guid createdBy)`; get-only props `Id, NameEn, NameAr, Unit, LowerBound, UpperBound, CreatedBy, CreatedAt`) and `IMedicalTestRepository` (`ListAsync`, `AddAsync`, `GetByIdAsync`, `RemoveAsync`, `SaveChangesAsync`).

- [ ] **Step 1: Write the failing test**

`backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/MedicalTestTests.cs`:
```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Entities;

public class MedicalTestTests
{
    [Fact]
    public void Ctor_assigns_id_fields_creator_and_timestamp()
    {
        var createdBy = Guid.NewGuid();

        var t = new MedicalTest(" Hemoglobin ", " الهيموغلوبين ", " g/dL ", 11m, 17.5m, createdBy);

        Assert.NotEqual(Guid.Empty, t.Id);
        Assert.Equal("Hemoglobin", t.NameEn);
        Assert.Equal("الهيموغلوبين", t.NameAr);
        Assert.Equal("g/dL", t.Unit);
        Assert.Equal(11m, t.LowerBound);
        Assert.Equal(17.5m, t.UpperBound);
        Assert.Equal(createdBy, t.CreatedBy);
        Assert.NotEqual(default, t.CreatedAt);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run (from `backend/`): `dotnet test tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~MedicalTestTests`
Expected: FAIL — `MedicalTest` does not exist (compile error).

- [ ] **Step 3: Write the entity**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Entities/MedicalTest.cs`:
```csharp
namespace Kheprx.BaseBackend.Health.Domain.Entities;

public sealed class MedicalTest
{
    public Guid Id { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    public decimal LowerBound { get; private set; }
    public decimal UpperBound { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private MedicalTest() { } // EF Core

    public MedicalTest(string nameEn, string nameAr, string unit,
        decimal lowerBound, decimal upperBound, Guid createdBy)
    {
        Id = Guid.NewGuid();
        NameEn = nameEn.Trim();
        NameAr = nameAr.Trim();
        Unit = unit.Trim();
        LowerBound = lowerBound;
        UpperBound = upperBound;
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 4: Write the repository port**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IMedicalTestRepository.cs`:
```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;

namespace Kheprx.BaseBackend.Health.Domain.Repositories;

public interface IMedicalTestRepository
{
    Task<IReadOnlyList<MedicalTest>> ListAsync(CancellationToken ct = default);
    Task AddAsync(MedicalTest test, CancellationToken ct = default);
    Task<MedicalTest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task RemoveAsync(MedicalTest test, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~MedicalTestTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities
git commit -m "feat(health): MedicalTest entity + repository port"
```

---

## Task B3: EF persistence (DbContext, factory, configuration, repository impl)

**Files:**
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Data/HealthDbContext.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Data/HealthDbContextFactory.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Configurations/MedicalTestConfiguration.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/MedicalTestRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/MedicalTestRepositoryTests.cs`

**Interfaces:**
- Consumes: `MedicalTest`, `IMedicalTestRepository` (Task B2).
- Produces: `HealthDbContext` (`DbSet<MedicalTest> MedicalTests`), `HealthDbContextFactory`, `MedicalTestRepository : IMedicalTestRepository`.

- [ ] **Step 1: Write the failing repository test**

`backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/MedicalTestRepositoryTests.cs`:
```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Kheprx.BaseBackend.Health.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Repositories;

public class MedicalTestRepositoryTests
{
    private static HealthDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<HealthDbContext>()
            .UseInMemoryDatabase($"health-{Guid.NewGuid()}")
            .Options;
        return new HealthDbContext(options);
    }

    [Fact]
    public async Task Add_then_List_returns_saved_rows_ordered_by_name()
    {
        await using var db = NewDb();
        var repo = new MedicalTestRepository(db);
        await repo.AddAsync(new MedicalTest("Uric Acid", "حمض اليوريك", "mg/dL", 1m, 7m, Guid.NewGuid()));
        await repo.AddAsync(new MedicalTest("Glucose", "الجلوكوز", "mg/dL", 70m, 110m, Guid.NewGuid()));
        await repo.SaveChangesAsync();

        var list = await repo.ListAsync();

        Assert.Equal(2, list.Count);
        Assert.Equal("Glucose", list[0].NameEn); // alphabetical
        Assert.Equal("Uric Acid", list[1].NameEn);
    }

    [Fact]
    public async Task GetById_returns_row_then_Remove_deletes_it()
    {
        await using var db = NewDb();
        var repo = new MedicalTestRepository(db);
        var test = new MedicalTest("Hemoglobin", "الهيموغلوبين", "g/dL", 11m, 17.5m, Guid.NewGuid());
        await repo.AddAsync(test);
        await repo.SaveChangesAsync();

        var found = await repo.GetByIdAsync(test.Id);
        Assert.NotNull(found);

        await repo.RemoveAsync(found!);
        await repo.SaveChangesAsync();

        Assert.Null(await repo.GetByIdAsync(test.Id));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~MedicalTestRepositoryTests`
Expected: FAIL — `HealthDbContext` / `MedicalTestRepository` do not exist.

- [ ] **Step 3: Write the DbContext**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Data/HealthDbContext.cs`:
```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Health.Infrastructure.Data;

public sealed class HealthDbContext : DbContext
{
    public HealthDbContext(DbContextOptions<HealthDbContext> options) : base(options) { }

    public DbSet<MedicalTest> MedicalTests => Set<MedicalTest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("health");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HealthDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Step 4: Write the design-time factory**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Data/HealthDbContextFactory.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kheprx.BaseBackend.Health.Infrastructure.Data;

// Design-time factory used by `dotnet ef`. Uses a local-dev connection string;
// at runtime the application uses ConnectionStrings:Postgres from configuration instead.
public sealed class HealthDbContextFactory : IDesignTimeDbContextFactory<HealthDbContext>
{
    public HealthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HealthDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=basebackend;Username=postgres;Password=postgres")
            .Options;
        return new HealthDbContext(options);
    }
}
```

- [ ] **Step 5: Write the entity configuration**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Configurations/MedicalTestConfiguration.cs`:
```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Health.Infrastructure.Configurations;

internal sealed class MedicalTestConfiguration : IEntityTypeConfiguration<MedicalTest>
{
    public void Configure(EntityTypeBuilder<MedicalTest> builder)
    {
        builder.ToTable("medical_test", "health");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(t => t.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Unit).HasMaxLength(50).IsRequired();
        builder.Property(t => t.LowerBound).HasColumnType("numeric(8,2)").IsRequired();
        builder.Property(t => t.UpperBound).HasColumnType("numeric(8,2)").IsRequired();
        builder.Property(t => t.CreatedBy).IsRequired();   // loose Guid — no cross-module FK
        builder.Property(t => t.CreatedAt).IsRequired();
    }
}
```

- [ ] **Step 6: Write the repository impl**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/MedicalTestRepository.cs`:
```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Health.Infrastructure.Repositories;

internal sealed class MedicalTestRepository : IMedicalTestRepository
{
    private readonly HealthDbContext _db;
    public MedicalTestRepository(HealthDbContext db) => _db = db;

    public async Task<IReadOnlyList<MedicalTest>> ListAsync(CancellationToken ct = default)
        => await _db.MedicalTests.AsNoTracking().OrderBy(t => t.NameEn).ToListAsync(ct);

    public async Task AddAsync(MedicalTest test, CancellationToken ct = default)
        => await _db.MedicalTests.AddAsync(test, ct);

    public Task<MedicalTest?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.MedicalTests.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task RemoveAsync(MedicalTest test, CancellationToken ct = default)
    {
        _db.MedicalTests.Remove(test);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~MedicalTestRepositoryTests`
Expected: PASS (both facts).

- [ ] **Step 8: Commit**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories
git commit -m "feat(health): HealthDbContext + medical_test EF mapping + repository"
```

---

## Task B4: EF migration for `health.medical_test`

**Files:**
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Migrations/*_CreateMedicalTestTable.cs` (+ `.Designer.cs` + `HealthDbContextModelSnapshot.cs`) — generated by `dotnet ef`.

**Interfaces:**
- Consumes: `HealthDbContext` + `MedicalTestConfiguration` (Task B3), `HealthDbContextFactory` (design-time).
- Produces: an applied-at-startup migration creating schema `health` + table `medical_test`.

- [ ] **Step 1: Stop the dev API server** (releases the DLL lock so `dotnet ef` can build).

- [ ] **Step 2: Generate the migration**

Run (from `backend/`):
```bash
dotnet ef migrations add CreateMedicalTestTable \
  --project src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure \
  --startup-project Kheprx.BaseBackend.Api \
  --context HealthDbContext \
  --output-dir Migrations
```
Expected: creates `Migrations/*_CreateMedicalTestTable.cs`, its `.Designer.cs`, and `HealthDbContextModelSnapshot.cs` under Health.Infrastructure.

- [ ] **Step 3: Verify the generated `Up` touches only `health.medical_test`**

Open the generated `*_CreateMedicalTestTable.cs`. Confirm `Up`:
- `migrationBuilder.EnsureSchema(name: "health");`
- `migrationBuilder.CreateTable(name: "medical_test", schema: "health", ...)` with columns `id (uuid, PK)`, `name_en (varchar(200), not null)`, `name_ar (varchar(200), not null)`, `unit (varchar(50), not null)`, `lower_bound (numeric(8,2), not null)`, `upper_bound (numeric(8,2), not null)`, `created_by (uuid, not null)`, `created_at (timestamptz, not null)`.
- **No** other tables/schemas, and **no** FK to `identity.app_user`.

If anything else appears, STOP and consult (do not hand-edit around unexpected model drift).

- [ ] **Step 4: Confirm the model has no pending changes**

Run: `dotnet ef migrations has-pending-model-changes --context HealthDbContext --project src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure --startup-project Kheprx.BaseBackend.Api`
Expected: "No changes have been made to the model since the last migration."

- [ ] **Step 5: Build (verify the migration compiles)**

Run: `dotnet build Kheprx.BaseBackend.sln`
Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Migrations
git commit -m "feat(health): initial migration creating health.medical_test"
```

---

## Task B5: Application layer (DTOs, messages, validator, service)

**Files:**
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/DTOs/MedicalTestDtos.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Resources/MedicalTestMessages.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Validators/CreateMedicalTestRequestValidator.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/Interfaces/IMedicalTestService.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/MedicalTestService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/CreateMedicalTestRequestValidatorTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/MedicalTestServiceTests.cs`

**Interfaces:**
- Consumes: `MedicalTest`, `IMedicalTestRepository` (Task B2).
- Produces:
  - `CreateMedicalTestRequest(string NameEn, string NameAr, string Unit, decimal LowerBound, decimal UpperBound)`
  - `MedicalTestDto(Guid Id, string NameEn, string NameAr, string Unit, decimal LowerBound, decimal UpperBound, DateTime CreatedAt)`
  - `IMedicalTestService`: `Task<IReadOnlyList<MedicalTestDto>> ListAsync(CancellationToken)`, `Task<MedicalTestDto> CreateAsync(CreateMedicalTestRequest request, Guid createdBy, CancellationToken)`, `Task<bool> DeleteAsync(Guid id, CancellationToken)`
  - `MedicalTestMessages.Success.{Listed,Created,Deleted}(string lang)`, `MedicalTestMessages.Errors.NotFound(string lang)`

- [ ] **Step 1: Write the DTOs**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/DTOs/MedicalTestDtos.cs`:
```csharp
namespace Kheprx.BaseBackend.Health.Application.DTOs;

/// <summary>Payload to create a medical test (POST /api/medical-tests).</summary>
public sealed record CreateMedicalTestRequest(
    string NameEn,
    string NameAr,
    string Unit,
    decimal LowerBound,
    decimal UpperBound);

/// <summary>A medical test catalog entry.</summary>
public sealed record MedicalTestDto(
    Guid Id,
    string NameEn,
    string NameAr,
    string Unit,
    decimal LowerBound,
    decimal UpperBound,
    DateTime CreatedAt);
```

- [ ] **Step 2: Write the messages**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Resources/MedicalTestMessages.cs`:
```csharp
namespace Kheprx.BaseBackend.Health.Application.Resources;

/// <summary>Localized messages for the medical-test catalog.</summary>
public static class MedicalTestMessages
{
    public static class Success
    {
        public static string Listed(string lang) => lang switch { "ar" => "قائمة الفحوصات الطبية", _ => "Medical tests" };
        public static string Created(string lang) => lang switch { "ar" => "تمت إضافة الفحص الطبي", _ => "Medical test added" };
        public static string Deleted(string lang) => lang switch { "ar" => "تم حذف الفحص الطبي", _ => "Medical test deleted" };
    }

    public static class Errors
    {
        public static string NotFound(string lang) => lang switch { "ar" => "الفحص الطبي غير موجود", _ => "Medical test not found" };
        public static string NameEnRequired(string lang) => lang switch { "ar" => "اسم الفحص (بالإنجليزية) مطلوب", _ => "Test name (EN) is required" };
        public static string NameArRequired(string lang) => lang switch { "ar" => "اسم الفحص (بالعربية) مطلوب", _ => "Test name (AR) is required" };
        public static string UnitRequired(string lang) => lang switch { "ar" => "الوحدة مطلوبة", _ => "Unit is required" };
        public static string UpperMustExceedLower(string lang) => lang switch { "ar" => "يجب أن يكون الحد الأعلى أكبر من الحد الأدنى", _ => "Upper bound must exceed lower bound" };
    }
}
```

- [ ] **Step 3: Write the failing validator test**

`backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/CreateMedicalTestRequestValidatorTests.cs`:
```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public class CreateMedicalTestRequestValidatorTests
{
    private static CreateMedicalTestRequest Valid() =>
        new(NameEn: "Hemoglobin", NameAr: "الهيموغلوبين", Unit: "g/dL", LowerBound: 11m, UpperBound: 17.5m);

    private readonly CreateMedicalTestRequestValidator _v = new();

    [Fact] public void Valid_request_passes() => Assert.True(_v.Validate(Valid()).IsValid);

    [Fact]
    public void Missing_required_text_fields_fail()
    {
        Assert.False(_v.Validate(Valid() with { NameEn = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { NameAr = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Unit = "" }).IsValid);
    }

    [Fact]
    public void Upper_bound_must_exceed_lower_bound()
    {
        Assert.False(_v.Validate(Valid() with { LowerBound = 10m, UpperBound = 10m }).IsValid); // equal
        Assert.False(_v.Validate(Valid() with { LowerBound = 20m, UpperBound = 5m }).IsValid);  // inverted
    }
}
```

- [ ] **Step 4: Run the validator test to verify it fails**

Run: `dotnet test tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~CreateMedicalTestRequestValidatorTests`
Expected: FAIL — `CreateMedicalTestRequestValidator` does not exist.

- [ ] **Step 5: Write the validator**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Validators/CreateMedicalTestRequestValidator.cs`:
```csharp
using FluentValidation;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Health.Application.Validators;

public sealed class CreateMedicalTestRequestValidator : AbstractValidator<CreateMedicalTestRequest>
{
    public CreateMedicalTestRequestValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(_ => MedicalTestMessages.Errors.NameEnRequired(AppLanguage.Current))
            .MaximumLength(200);
        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage(_ => MedicalTestMessages.Errors.NameArRequired(AppLanguage.Current))
            .MaximumLength(200);
        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage(_ => MedicalTestMessages.Errors.UnitRequired(AppLanguage.Current))
            .MaximumLength(50);
        RuleFor(x => x.UpperBound)
            .GreaterThan(x => x.LowerBound)
            .WithMessage(_ => MedicalTestMessages.Errors.UpperMustExceedLower(AppLanguage.Current));
    }
}
```

- [ ] **Step 6: Run the validator test to verify it passes**

Run: `dotnet test tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~CreateMedicalTestRequestValidatorTests`
Expected: PASS.

- [ ] **Step 7: Write the service interface**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/Interfaces/IMedicalTestService.cs`:
```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;

namespace Kheprx.BaseBackend.Health.Application.Services.Interfaces;

public interface IMedicalTestService
{
    Task<IReadOnlyList<MedicalTestDto>> ListAsync(CancellationToken ct = default);
    Task<MedicalTestDto> CreateAsync(CreateMedicalTestRequest request, Guid createdBy, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 8: Write the failing service test**

`backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/MedicalTestServiceTests.cs`:
```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Services;

public class MedicalTestServiceTests
{
    private static CreateMedicalTestRequest Req() =>
        new("Hemoglobin", "الهيموغلوبين", "g/dL", 11m, 17.5m);

    [Fact]
    public async Task Create_persists_entity_with_creator_and_returns_dto()
    {
        var repo = new Mock<IMedicalTestRepository>();
        MedicalTest? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<MedicalTest>(), It.IsAny<CancellationToken>()))
            .Callback<MedicalTest, CancellationToken>((t, _) => added = t)
            .Returns(Task.CompletedTask);
        var svc = new MedicalTestService(repo.Object);
        var createdBy = Guid.NewGuid();

        var dto = await svc.CreateAsync(Req(), createdBy);

        Assert.NotNull(added);
        Assert.Equal(createdBy, added!.CreatedBy);
        Assert.Equal("Hemoglobin", dto.NameEn);
        Assert.Equal("الهيموغلوبين", dto.NameAr);
        Assert.Equal(11m, dto.LowerBound);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_maps_rows_to_dtos()
    {
        var repo = new Mock<IMedicalTestRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new MedicalTest("Glucose", "الجلوكوز", "mg/dL", 70m, 110m, Guid.NewGuid()) });
        var svc = new MedicalTestService(repo.Object);

        var list = await svc.ListAsync();

        Assert.Single(list);
        Assert.Equal("Glucose", list[0].NameEn);
        Assert.Equal(110m, list[0].UpperBound);
    }

    [Fact]
    public async Task Delete_returns_false_when_missing_and_true_when_present()
    {
        var repo = new Mock<IMedicalTestRepository>();
        var svc = new MedicalTestService(repo.Object);
        var id = Guid.NewGuid();

        repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((MedicalTest?)null);
        Assert.False(await svc.DeleteAsync(id));

        var existing = new MedicalTest("Uric Acid", "حمض اليوريك", "mg/dL", 1m, 7m, Guid.NewGuid());
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        Assert.True(await svc.DeleteAsync(existing.Id));
        repo.Verify(r => r.RemoveAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 9: Run the service test to verify it fails**

Run: `dotnet test tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~MedicalTestServiceTests`
Expected: FAIL — `MedicalTestService` does not exist.

- [ ] **Step 10: Write the service**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/MedicalTestService.cs`:
```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;

namespace Kheprx.BaseBackend.Health.Application.Services;

internal sealed class MedicalTestService : IMedicalTestService
{
    private readonly IMedicalTestRepository _tests;
    public MedicalTestService(IMedicalTestRepository tests) => _tests = tests;

    public async Task<IReadOnlyList<MedicalTestDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await _tests.ListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<MedicalTestDto> CreateAsync(CreateMedicalTestRequest request, Guid createdBy, CancellationToken ct = default)
    {
        var test = new MedicalTest(request.NameEn, request.NameAr, request.Unit,
            request.LowerBound, request.UpperBound, createdBy);
        await _tests.AddAsync(test, ct);
        await _tests.SaveChangesAsync(ct);
        return ToDto(test);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var test = await _tests.GetByIdAsync(id, ct);
        if (test is null) return false;
        await _tests.RemoveAsync(test, ct);
        await _tests.SaveChangesAsync(ct);
        return true;
    }

    private static MedicalTestDto ToDto(MedicalTest t) =>
        new(t.Id, t.NameEn, t.NameAr, t.Unit, t.LowerBound, t.UpperBound, t.CreatedAt);
}
```

- [ ] **Step 11: Run all Health tests to verify they pass**

Run: `dotnet test tests/Kheprx.BaseBackend.Health.UnitTests`
Expected: PASS (entity, repository, validator, service).

- [ ] **Step 12: Commit**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services
git commit -m "feat(health): medical-test DTOs, messages, validator, service"
```

---

## Task B6: API surface + module wiring + seeder

**Files:**
- Create: `backend/Kheprx.BaseBackend.Api/Controllers/MedicalTestsController.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Extensions/HealthModuleExtensions.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Data/HealthSeeder.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Program.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Data/HealthSeederTests.cs`

**Interfaces:**
- Consumes: `IMedicalTestService`, `MedicalTestDto`, `CreateMedicalTestRequest`, `MedicalTestMessages` (B5); `HealthDbContext`, `MedicalTestRepository` (B3); `IMedicalTestRepository` (B2); `BaseApiController.CurrentUserId()`.
- Produces: `MedicalTestsController` (`GET`/`POST`/`DELETE api/medical-tests`), `HealthModuleExtensions.AddHealthModule(...)`, `MigrationExtensions.ApplyHealthMigrationsAsync(...)`, `HealthSeeder.SeedAsync(HealthDbContext)`.

- [ ] **Step 1: Write the failing seeder test**

`backend/tests/Kheprx.BaseBackend.Health.UnitTests/Data/HealthSeederTests.cs`:
```csharp
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Data;

public class HealthSeederTests
{
    private static HealthDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<HealthDbContext>()
            .UseInMemoryDatabase($"health-seed-{Guid.NewGuid()}")
            .Options;
        return new HealthDbContext(options);
    }

    [Fact]
    public async Task Seeds_three_demo_tests_and_is_idempotent()
    {
        await using var db = NewDb();

        await HealthSeeder.SeedAsync(db);
        await HealthSeeder.SeedAsync(db); // second run must not duplicate

        Assert.Equal(3, await db.MedicalTests.CountAsync());
    }
}
```

- [ ] **Step 2: Run the seeder test to verify it fails**

Run: `dotnet test tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~HealthSeederTests`
Expected: FAIL — `HealthSeeder` does not exist.

- [ ] **Step 3: Write the seeder**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Data/HealthSeeder.cs`:
```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Health.Infrastructure.Data;

public static class HealthSeeder
{
    // Demo `created_by` — a fixed, documented sentinel. medical_test.created_by has no FK
    // (cross-module decoupling), so this need not reference a real identity.app_user row.
    private static readonly Guid SeedCreatedBy = new("00000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(HealthDbContext db, CancellationToken ct = default)
    {
        if (await db.MedicalTests.AnyAsync(ct)) return;

        await db.MedicalTests.AddRangeAsync(new[]
        {
            new MedicalTest("Hemoglobin", "الهيموغلوبين", "g/dL", 11m, 17.5m, SeedCreatedBy),
            new MedicalTest("Glucose (FBS)", "الجلوكوز الصائم", "mg/dL", 70m, 110m, SeedCreatedBy),
            new MedicalTest("Uric Acid", "حمض اليوريك", "mg/dL", 1m, 7m, SeedCreatedBy),
        }, ct);
        await db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Run the seeder test to verify it passes**

Run: `dotnet test tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~HealthSeederTests`
Expected: PASS.

- [ ] **Step 5: Write the module registration extension**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Extensions/HealthModuleExtensions.cs`:
```csharp
using Kheprx.BaseBackend.Health.Application.Services;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Kheprx.BaseBackend.Health.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kheprx.BaseBackend.Health.Infrastructure.Extensions;

public static class HealthModuleExtensions
{
    public static IServiceCollection AddHealthModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<HealthDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<IMedicalTestRepository, MedicalTestRepository>();
        services.AddScoped<IMedicalTestService, MedicalTestService>();
        return services;
    }
}
```

- [ ] **Step 6: Write the controller**

`backend/Kheprx.BaseBackend.Api/Controllers/MedicalTestsController.cs`:
```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/medical-tests")]
[Authorize(Roles = "head_coach")]
public sealed class MedicalTestsController : BaseApiController
{
    private readonly IMedicalTestService _service;
    public MedicalTestsController(IMedicalTestService service) => _service = service;

    /// <summary>Lists the medical-test catalog.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MedicalTestDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MedicalTestDto>>>> List(CancellationToken ct)
    {
        var tests = await _service.ListAsync(ct);
        var body = ApiResponse<IReadOnlyList<MedicalTestDto>>.Success(
            MedicalTestMessages.Success.Listed(AppLanguage.Current), tests);
        return Ok(body);
    }

    /// <summary>Adds a medical test. Head Coach only.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MedicalTestDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<MedicalTestDto>>> Create(CreateMedicalTestRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, CurrentUserId(), ct);
        var body = ApiResponse<MedicalTestDto>.Success(
            MedicalTestMessages.Success.Created(AppLanguage.Current), created);
        return StatusCode(StatusCodes.Status201Created, body);
    }

    /// <summary>Deletes a medical test by id. Head Coach only.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<Guid>>> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, ct);
        if (!deleted)
        {
            var notFound = ApiResponse<Guid>.Failure(MedicalTestMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, notFound);
        }
        var body = ApiResponse<Guid>.Success(MedicalTestMessages.Success.Deleted(AppLanguage.Current), id);
        return Ok(body);
    }
}
```

- [ ] **Step 7: Wire the module into `Program.cs`**

In `backend/Kheprx.BaseBackend.Api/Program.cs`:
1. Add the using near the existing Identity using:
```csharp
using Kheprx.BaseBackend.Health.Infrastructure.Extensions;
```
2. Register the module immediately after `AddIdentityModule` (BEFORE `AddFluentValidationConfiguration`):
```csharp
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddHealthModule(builder.Configuration);
```
3. Apply Health migrations right after the identity ones:
```csharp
await app.ApplyIdentityMigrationsAsync();
await app.ApplyHealthMigrationsAsync();
```

- [ ] **Step 8: Add `ApplyHealthMigrationsAsync` to `MigrationExtensions`**

In `backend/Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs`, add the using and a new method beside `ApplyIdentityMigrationsAsync`:
```csharp
using Kheprx.BaseBackend.Health.Infrastructure.Data;
```
```csharp
    public static async Task ApplyHealthMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HealthDbContext>();
        await db.Database.MigrateAsync();
        await HealthSeeder.SeedAsync(db);
    }
```

- [ ] **Step 9: Build + run the full backend test suite**

Run (from `backend/`, dev server stopped):
```bash
dotnet build Kheprx.BaseBackend.sln
dotnet test tests/Kheprx.BaseBackend.Health.UnitTests
```
Expected: build succeeds; all Health tests pass.

- [ ] **Step 10: Smoke-test the API manually**

Start the API (`dotnet run --project Kheprx.BaseBackend.Api`). Confirm at startup the `health.medical_test` table is created and seeded (3 rows). With a **head_coach** JWT: `GET /api/medical-tests` returns the 3 seeded tests; `POST /api/medical-tests` with `{ "nameEn":"Vitamin D", "nameAr":"فيتامين د", "unit":"ng/mL", "lowerBound":30, "upperBound":100 }` returns 201; `DELETE /api/medical-tests/{id}` returns 200, and a random id returns 404. With a **captain** JWT, all three return 403. Stop the server when done.

- [ ] **Step 11: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Extensions backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Data/HealthSeeder.cs backend/tests/Kheprx.BaseBackend.Health.UnitTests/Data
git commit -m "feat(health): medical-tests API, module wiring, startup migration + seed"
```

---

## Task F1: Frontend data slice (DTOs, model, repository port + impl + providers)

**Files:**
- Create: `frontend/src/app/features/medical-tests/data/dto/medical-test.dto.ts`
- Create: `frontend/src/app/features/medical-tests/domain/model/medical-test.ts`
- Create: `frontend/src/app/features/medical-tests/domain/repositories/medical-test.repository.ts`
- Create: `frontend/src/app/features/medical-tests/data/repositories/medical-test.repository.impl.ts`
- Create: `frontend/src/app/features/medical-tests/data/medical-test.providers.ts`
- Test: `frontend/src/app/features/medical-tests/testing/data/repositories/medical-test.repository.impl.spec.ts`

**Interfaces:**
- Produces:
  - DTOs: `MedicalTestDtoRs { id; nameEn; nameAr; unit; lowerBound; upperBound; createdAt }`, `MedicalTestListDtoRs extends BaseResponseRs<MedicalTestDtoRs[]>`, `MedicalTestItemDtoRs extends BaseResponseRs<MedicalTestDtoRs>`, `MedicalTestDeletedDtoRs extends BaseResponseRs<unknown>`, `CreateMedicalTestDtoRq { nameEn; nameAr; unit; lowerBound: number; upperBound: number }`, `isMedicalTestDtoRsValid`.
  - Model: `MedicalTest { id; nameEn; nameAr; unit; lowerBound; upperBound; createdAt }`.
  - `IMedicalTestRepository { list(); create(rq); delete(id) }`, token `MEDICAL_TEST_REPOSITORY`.
  - `MedicalTestRepositoryImpl`, `MEDICAL_TEST_PROVIDERS`.

- [ ] **Step 1: Write the DTOs**

`frontend/src/app/features/medical-tests/data/dto/medical-test.dto.ts`:
```ts
// medical-test.dto.ts — medical-test catalog response/request DTOs (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface MedicalTestDtoRs {
  id: string;
  nameEn: string;
  nameAr: string;
  unit: string;
  lowerBound: number;
  upperBound: number;
  createdAt: string;
}

export interface MedicalTestListDtoRs extends BaseResponseRs<MedicalTestDtoRs[]> {}
export interface MedicalTestItemDtoRs extends BaseResponseRs<MedicalTestDtoRs> {}
export interface MedicalTestDeletedDtoRs extends BaseResponseRs<unknown> {}

export interface CreateMedicalTestDtoRq {
  nameEn: string;
  nameAr: string;
  unit: string;
  lowerBound: number;
  upperBound: number;
}

export function isMedicalTestDtoRsValid(dto: unknown): dto is MedicalTestDtoRs {
  const d = dto as MedicalTestDtoRs;
  return !!d && typeof d.id === 'string'
    && typeof d.nameEn === 'string' && typeof d.nameAr === 'string'
    && typeof d.unit === 'string'
    && typeof d.lowerBound === 'number' && typeof d.upperBound === 'number';
}
```

- [ ] **Step 2: Write the domain model**

`frontend/src/app/features/medical-tests/domain/model/medical-test.ts`:
```ts
export interface MedicalTest {
  id: string;
  nameEn: string;
  nameAr: string;
  unit: string;
  lowerBound: number;
  upperBound: number;
  createdAt: string;
}
```

- [ ] **Step 3: Write the repository port**

`frontend/src/app/features/medical-tests/domain/repositories/medical-test.repository.ts`:
```ts
import { InjectionToken } from '@angular/core';
import {
  MedicalTestListDtoRs,
  MedicalTestItemDtoRs,
  MedicalTestDeletedDtoRs,
  CreateMedicalTestDtoRq,
} from '@features/medical-tests/data/dto/medical-test.dto';

export interface IMedicalTestRepository {
  list(): Promise<MedicalTestListDtoRs>;
  create(rq: CreateMedicalTestDtoRq): Promise<MedicalTestItemDtoRs>;
  delete(id: string): Promise<MedicalTestDeletedDtoRs>;
}

export const MEDICAL_TEST_REPOSITORY = new InjectionToken<IMedicalTestRepository>('MEDICAL_TEST_REPOSITORY');
```

- [ ] **Step 4: Write the failing repository-impl spec**

`frontend/src/app/features/medical-tests/testing/data/repositories/medical-test.repository.impl.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { MedicalTestRepositoryImpl } from '@features/medical-tests/data/repositories/medical-test.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('MedicalTestRepositoryImpl', () => {
  const http = { get: jest.fn(), post: jest.fn(), delete: jest.fn() } as unknown as HttpClientService;
  let repo: MedicalTestRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [MedicalTestRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(MedicalTestRepositoryImpl);
  });

  it('list GETs /api/medical-tests', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.list();
    expect(http.get).toHaveBeenCalledWith('/api/medical-tests');
  });

  it('create POSTs /api/medical-tests with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: {} });
    const rq = { nameEn: 'Vit D', nameAr: 'د', unit: 'ng/mL', lowerBound: 30, upperBound: 100 };
    await repo.create(rq);
    expect(http.post).toHaveBeenCalledWith('/api/medical-tests', { body: rq });
  });

  it('delete DELETEs /api/medical-tests/{id}', async () => {
    (http.delete as jest.Mock).mockResolvedValue({ data: 'abc' });
    await repo.delete('abc');
    expect(http.delete).toHaveBeenCalledWith('/api/medical-tests/abc');
  });
});
```

- [ ] **Step 5: Run the spec to verify it fails**

Run (from `frontend/`): `npx jest medical-test.repository.impl`
Expected: FAIL — `MedicalTestRepositoryImpl` does not exist.

- [ ] **Step 6: Write the repository impl**

`frontend/src/app/features/medical-tests/data/repositories/medical-test.repository.impl.ts`:
```ts
// medical-test.repository.impl.ts — medical-tests repository. Returns the API DTO envelope.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IMedicalTestRepository } from '@features/medical-tests/domain/repositories/medical-test.repository';
import {
  MedicalTestListDtoRs,
  MedicalTestItemDtoRs,
  MedicalTestDeletedDtoRs,
  CreateMedicalTestDtoRq,
} from '@features/medical-tests/data/dto/medical-test.dto';

@Injectable({ providedIn: 'root' })
export class MedicalTestRepositoryImpl implements IMedicalTestRepository {
  private readonly http = inject(HttpClientService);

  list(): Promise<MedicalTestListDtoRs> {
    return this.http.get<MedicalTestListDtoRs>('/api/medical-tests');
  }

  create(rq: CreateMedicalTestDtoRq): Promise<MedicalTestItemDtoRs> {
    return this.http.post<MedicalTestItemDtoRs>('/api/medical-tests', { body: rq });
  }

  delete(id: string): Promise<MedicalTestDeletedDtoRs> {
    return this.http.delete<MedicalTestDeletedDtoRs>('/api/medical-tests/' + id);
  }
}
```

- [ ] **Step 7: Write the providers**

`frontend/src/app/features/medical-tests/data/medical-test.providers.ts`:
```ts
import { Provider } from '@angular/core';
import { MEDICAL_TEST_REPOSITORY } from '@features/medical-tests/domain/repositories/medical-test.repository';
import { MedicalTestRepositoryImpl } from '@features/medical-tests/data/repositories/medical-test.repository.impl';

// Live wiring: bind the medical-tests repository port to the HTTP impl (/api/medical-tests).
export const MEDICAL_TEST_PROVIDERS: Provider[] = [
  { provide: MEDICAL_TEST_REPOSITORY, useClass: MedicalTestRepositoryImpl },
];
```

- [ ] **Step 8: Run the spec to verify it passes**

Run: `npx jest medical-test.repository.impl`
Expected: PASS (all three cases).

- [ ] **Step 9: Commit**

```bash
git add frontend/src/app/features/medical-tests/data frontend/src/app/features/medical-tests/domain/model frontend/src/app/features/medical-tests/domain/repositories frontend/src/app/features/medical-tests/testing
git commit -m "feat(medical-tests): data slice (dto, model, repository)"
```

---

## Task F2: Frontend use cases (list, create, delete)

**Files:**
- Create: `frontend/src/app/features/medical-tests/domain/usecases/list-medical-tests.use-case.ts`
- Create: `frontend/src/app/features/medical-tests/domain/usecases/create-medical-test.use-case.ts`
- Create: `frontend/src/app/features/medical-tests/domain/usecases/delete-medical-test.use-case.ts`
- Test: `frontend/src/app/features/medical-tests/testing/domain/usecases/list-medical-tests.use-case.spec.ts`
- Test: `frontend/src/app/features/medical-tests/testing/domain/usecases/create-medical-test.use-case.spec.ts`

**Interfaces:**
- Consumes: `MEDICAL_TEST_REPOSITORY`, DTOs, `MedicalTest` (F1).
- Produces: `ListMedicalTestsUseCase` (`run(): Result<MedicalTest[]>`), `CreateMedicalTestUseCase` (`run(CreateMedicalTestDtoRq): Result<MedicalTest>`), `DeleteMedicalTestUseCase` (`run(id: string): Result<void>`).

- [ ] **Step 1: Write the failing use-case specs**

`frontend/src/app/features/medical-tests/testing/domain/usecases/list-medical-tests.use-case.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { ListMedicalTestsUseCase } from '@features/medical-tests/domain/usecases/list-medical-tests.use-case';
import { MEDICAL_TEST_REPOSITORY, IMedicalTestRepository } from '@features/medical-tests/domain/repositories/medical-test.repository';

function makeRepo(overrides: Partial<IMedicalTestRepository> = {}): IMedicalTestRepository {
  return {
    list: async () => ({ data: [
      { id: '1', nameEn: 'Hemoglobin', nameAr: 'هيموغلوبين', unit: 'g/dL', lowerBound: 11, upperBound: 17.5, createdAt: '2026-09-18T00:00:00Z' },
    ] }),
    create: async () => ({ data: {} as never }),
    delete: async () => ({ data: null }),
    ...overrides,
  } as unknown as IMedicalTestRepository;
}

function build(repo: IMedicalTestRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: MEDICAL_TEST_REPOSITORY, useValue: repo }] });
  return TestBed.inject(ListMedicalTestsUseCase);
}

describe('ListMedicalTestsUseCase', () => {
  it('maps DTOs to domain models', async () => {
    const uc = build(makeRepo());
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) {
      expect(r.data).toHaveLength(1);
      expect(r.data[0].nameEn).toBe('Hemoglobin');
      expect(r.data[0].upperBound).toBe(17.5);
    }
  });

  it('drops malformed rows', async () => {
    const uc = build(makeRepo({ list: async () => ({ data: [{ id: 'x' } as never] }) }));
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data).toHaveLength(0);
  });
});
```

`frontend/src/app/features/medical-tests/testing/domain/usecases/create-medical-test.use-case.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { CreateMedicalTestUseCase } from '@features/medical-tests/domain/usecases/create-medical-test.use-case';
import { MEDICAL_TEST_REPOSITORY, IMedicalTestRepository } from '@features/medical-tests/domain/repositories/medical-test.repository';

const RQ = { nameEn: 'Vit D', nameAr: 'فيتامين د', unit: 'ng/mL', lowerBound: 30, upperBound: 100 };

function build(repo: IMedicalTestRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: MEDICAL_TEST_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CreateMedicalTestUseCase);
}

describe('CreateMedicalTestUseCase', () => {
  it('maps the created DTO to a model', async () => {
    const repo = { create: async () => ({ data: { id: 'n1', ...RQ, createdAt: '2026-09-18T00:00:00Z' } }) } as unknown as IMedicalTestRepository;
    const uc = build(repo);
    const r = await uc.run(RQ);
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data.id).toBe('n1');
  });

  it('fails validation when the response is malformed', async () => {
    const repo = { create: async () => ({ data: { id: 'n1' } }) } as unknown as IMedicalTestRepository;
    const uc = build(repo);
    const r = await uc.run(RQ);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
```

- [ ] **Step 2: Run the specs to verify they fail**

Run: `npx jest medical-tests/testing/domain/usecases`
Expected: FAIL — the use cases do not exist.

- [ ] **Step 3: Write the list use case**

`frontend/src/app/features/medical-tests/domain/usecases/list-medical-tests.use-case.ts`:
```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { MEDICAL_TEST_REPOSITORY } from '@features/medical-tests/domain/repositories/medical-test.repository';
import { MedicalTest } from '@features/medical-tests/domain/model/medical-test';
import { MedicalTestDtoRs, isMedicalTestDtoRsValid } from '@features/medical-tests/data/dto/medical-test.dto';

// ListMedicalTestsUseCase: fetches the catalog (GET /api/medical-tests). Drops any row that
// fails validation, so a single malformed item never blanks the whole list.
@Injectable({ providedIn: 'root' })
export class ListMedicalTestsUseCase extends UseCase<void, MedicalTest[]> {
  private readonly repo = inject(MEDICAL_TEST_REPOSITORY);
  constructor() { super('ListMedicalTests'); }

  protected async execute(): Promise<MedicalTest[]> {
    const res = await this.repo.list();
    const items = Array.isArray(res.data) ? res.data : [];
    return items.filter(isMedicalTestDtoRsValid).map(toModel);
  }
}

function toModel(d: MedicalTestDtoRs): MedicalTest {
  return {
    id: d.id,
    nameEn: d.nameEn,
    nameAr: d.nameAr,
    unit: d.unit,
    lowerBound: d.lowerBound,
    upperBound: d.upperBound,
    createdAt: d.createdAt,
  };
}
```

- [ ] **Step 4: Write the create use case**

`frontend/src/app/features/medical-tests/domain/usecases/create-medical-test.use-case.ts`:
```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { MEDICAL_TEST_REPOSITORY } from '@features/medical-tests/domain/repositories/medical-test.repository';
import { CreateMedicalTestDtoRq, isMedicalTestDtoRsValid } from '@features/medical-tests/data/dto/medical-test.dto';
import { MedicalTest } from '@features/medical-tests/domain/model/medical-test';

@Injectable({ providedIn: 'root' })
export class CreateMedicalTestUseCase extends UseCase<CreateMedicalTestDtoRq, MedicalTest> {
  private readonly repo = inject(MEDICAL_TEST_REPOSITORY);
  constructor() { super('CreateMedicalTest'); }

  protected async execute(input: CreateMedicalTestDtoRq): Promise<MedicalTest> {
    const res = await this.repo.create(input);
    if (!isMedicalTestDtoRsValid(res.data)) throw new AppError('Invalid created medical test received', 'validation');
    const d = res.data;
    return {
      id: d.id, nameEn: d.nameEn, nameAr: d.nameAr, unit: d.unit,
      lowerBound: d.lowerBound, upperBound: d.upperBound, createdAt: d.createdAt,
    };
  }
}
```

- [ ] **Step 5: Write the delete use case**

`frontend/src/app/features/medical-tests/domain/usecases/delete-medical-test.use-case.ts`:
```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { MEDICAL_TEST_REPOSITORY } from '@features/medical-tests/domain/repositories/medical-test.repository';

// DeleteMedicalTestUseCase: DELETE /api/medical-tests/{id}. A 404 surfaces as an AppError
// with status 404 (the repository/http layer maps it), which the view model handles.
@Injectable({ providedIn: 'root' })
export class DeleteMedicalTestUseCase extends UseCase<string, void> {
  private readonly repo = inject(MEDICAL_TEST_REPOSITORY);
  constructor() { super('DeleteMedicalTest'); }

  protected async execute(id: string): Promise<void> {
    await this.repo.delete(id);
  }
}
```

- [ ] **Step 6: Run the specs to verify they pass**

Run: `npx jest medical-tests/testing/domain/usecases`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/app/features/medical-tests/domain/usecases frontend/src/app/features/medical-tests/testing/domain
git commit -m "feat(medical-tests): list/create/delete use cases"
```

---

## Task F3: Page + view model

**Files:**
- Create: `frontend/src/app/features/captain-panel/presentation/pages/medical-tests/medical-tests.viewmodel.ts`
- Create: `frontend/src/app/features/captain-panel/presentation/pages/medical-tests/medical-tests.page.ts`
- Create: `frontend/src/app/features/captain-panel/presentation/pages/medical-tests/medical-tests.page.html`
- Modify: `frontend/src/app/features/captain-panel/index.ts`
- Test: `frontend/src/app/features/captain-panel/testing/presentation/pages/medical-tests/medical-tests.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `ListMedicalTestsUseCase`, `CreateMedicalTestUseCase`, `DeleteMedicalTestUseCase` (F2), `MedicalTest` (F1), `NotificationService`, `TranslateService`, `LanguageStore`, `TextFieldComponent`, `TranslatePipe`.
- Produces: `MedicalTestsViewModel` (signals `tests, loading, error, nameEn, nameAr, unit, lowerBound, upperBound, submitting`; `canSubmit`; methods `load(), submit(), remove(id)`), `MedicalTestsPage`. Exported from `@features/captain-panel`.

- [ ] **Step 1: Write the failing view-model spec**

`frontend/src/app/features/captain-panel/testing/presentation/pages/medical-tests/medical-tests.viewmodel.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { MedicalTestsViewModel } from '@features/captain-panel/presentation/pages/medical-tests/medical-tests.viewmodel';
import { ListMedicalTestsUseCase } from '@features/medical-tests/domain/usecases/list-medical-tests.use-case';
import { CreateMedicalTestUseCase } from '@features/medical-tests/domain/usecases/create-medical-test.use-case';
import { DeleteMedicalTestUseCase } from '@features/medical-tests/domain/usecases/delete-medical-test.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

const test1 = { id: '1', nameEn: 'Hemoglobin', nameAr: 'هيموغلوبين', unit: 'g/dL', lowerBound: 11, upperBound: 17.5, createdAt: 'x' };

function build(over: {
  list?: unknown; create?: unknown; del?: unknown;
} = {}) {
  const listUc = { run: jest.fn().mockResolvedValue(over.list ?? { ok: true, data: [test1] }) };
  const createUc = { run: jest.fn().mockResolvedValue(over.create ?? { ok: true, data: { ...test1, id: '2', nameEn: 'Glucose' } }) };
  const deleteUc = { run: jest.fn().mockResolvedValue(over.del ?? { ok: true, data: undefined }) };
  const notify = { success: jest.fn(), error: jest.fn() };
  const i18n = { t: (k: string) => k };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [
    MedicalTestsViewModel,
    { provide: ListMedicalTestsUseCase, useValue: listUc },
    { provide: CreateMedicalTestUseCase, useValue: createUc },
    { provide: DeleteMedicalTestUseCase, useValue: deleteUc },
    { provide: NotificationService, useValue: notify },
    { provide: TranslateService, useValue: i18n },
  ] });
  return { vm: TestBed.inject(MedicalTestsViewModel), listUc, createUc, deleteUc, notify };
}

describe('MedicalTestsViewModel', () => {
  it('loads tests on construction', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    expect(vm.tests()).toHaveLength(1);
    expect(vm.tests()[0].nameEn).toBe('Hemoglobin');
  });

  it('canSubmit requires all fields and upper > lower', async () => {
    const { vm } = build();
    expect(vm.canSubmit()).toBe(false);
    vm.nameEn.set('Glucose'); vm.nameAr.set('جلوكوز'); vm.unit.set('mg/dL');
    vm.lowerBound.set('70'); vm.upperBound.set('60'); // upper < lower
    expect(vm.canSubmit()).toBe(false);
    vm.upperBound.set('110');
    expect(vm.canSubmit()).toBe(true);
  });

  it('submit prepends the created test and resets the form', async () => {
    const { vm, notify } = build();
    await Promise.resolve(); await Promise.resolve();
    vm.nameEn.set('Glucose'); vm.nameAr.set('جلوكوز'); vm.unit.set('mg/dL');
    vm.lowerBound.set('70'); vm.upperBound.set('110');
    await vm.submit();
    expect(vm.tests()[0].nameEn).toBe('Glucose'); // prepended
    expect(vm.nameEn()).toBe('');                  // reset
    expect(notify.success).toHaveBeenCalled();
  });

  it('remove drops the row on success', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    await vm.remove('1');
    expect(vm.tests()).toHaveLength(0);
  });
});
```

- [ ] **Step 2: Run the spec to verify it fails**

Run: `npx jest medical-tests.viewmodel`
Expected: FAIL — `MedicalTestsViewModel` does not exist.

- [ ] **Step 3: Write the view model**

`frontend/src/app/features/captain-panel/presentation/pages/medical-tests/medical-tests.viewmodel.ts`:
```ts
import { Injectable, computed, inject, signal } from '@angular/core';
import { ListMedicalTestsUseCase } from '@features/medical-tests/domain/usecases/list-medical-tests.use-case';
import { CreateMedicalTestUseCase } from '@features/medical-tests/domain/usecases/create-medical-test.use-case';
import { DeleteMedicalTestUseCase } from '@features/medical-tests/domain/usecases/delete-medical-test.use-case';
import { MedicalTest } from '@features/medical-tests/domain/model/medical-test';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

@Injectable()
export class MedicalTestsViewModel {
  private readonly listTests = inject(ListMedicalTestsUseCase);
  private readonly createTest = inject(CreateMedicalTestUseCase);
  private readonly deleteTest = inject(DeleteMedicalTestUseCase);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly tests = signal<MedicalTest[]>([]);
  readonly loading = signal(false);
  readonly error = signal(false);

  readonly nameEn = signal('');
  readonly nameAr = signal('');
  readonly unit = signal('');
  readonly lowerBound = signal('');
  readonly upperBound = signal('');
  readonly submitting = signal(false);

  readonly canSubmit = computed(() => {
    const lo = Number(this.lowerBound());
    const hi = Number(this.upperBound());
    return this.nameEn().trim().length > 0
      && this.nameAr().trim().length > 0
      && this.unit().trim().length > 0
      && this.lowerBound().trim().length > 0
      && this.upperBound().trim().length > 0
      && Number.isFinite(lo) && Number.isFinite(hi)
      && hi > lo;
  });

  constructor() { void this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const r = await this.listTests.run();
    this.loading.set(false);
    if (r.ok) this.tests.set(r.data);
    else this.error.set(true);
  }

  async submit(): Promise<void> {
    if (!this.canSubmit() || this.submitting()) return;
    this.submitting.set(true);
    const r = await this.createTest.run({
      nameEn: this.nameEn().trim(),
      nameAr: this.nameAr().trim(),
      unit: this.unit().trim(),
      lowerBound: Number(this.lowerBound()),
      upperBound: Number(this.upperBound()),
    });
    this.submitting.set(false);
    if (r.ok) {
      this.tests.update((list) => [r.data, ...list]);
      this.resetForm();
      this.notify.success(this.i18n.t('medicalTests.toasts.created'));
    } else {
      this.notify.error(this.i18n.t('medicalTests.toasts.createFailed'));
    }
  }

  async remove(id: string): Promise<void> {
    const r = await this.deleteTest.run(id);
    if (r.ok) {
      this.tests.update((list) => list.filter((t) => t.id !== id));
      this.notify.success(this.i18n.t('medicalTests.toasts.deleted'));
    } else if (r.error.status === 404) {
      await this.load();
      this.notify.error(this.i18n.t('medicalTests.toasts.alreadyGone'));
    } else {
      this.notify.error(this.i18n.t('medicalTests.toasts.deleteFailed'));
    }
  }

  private resetForm(): void {
    for (const s of [this.nameEn, this.nameAr, this.unit, this.lowerBound, this.upperBound]) s.set('');
  }
}
```

- [ ] **Step 4: Run the spec to verify it passes**

Run: `npx jest medical-tests.viewmodel`
Expected: PASS (all four cases).

- [ ] **Step 5: Write the page component**

`frontend/src/app/features/captain-panel/presentation/pages/medical-tests/medical-tests.page.ts`:
```ts
import { Component, inject } from '@angular/core';
import { TranslatePipe, LanguageStore } from '@core/i18n';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { MedicalTest } from '@features/medical-tests/domain/model/medical-test';
import { MedicalTestsViewModel } from './medical-tests.viewmodel';

@Component({
  selector: 'app-medical-tests-page',
  standalone: true,
  imports: [TranslatePipe, TextFieldComponent],
  templateUrl: './medical-tests.page.html',
})
export class MedicalTestsPage {
  protected readonly vm = inject(MedicalTestsViewModel);
  private readonly lang = inject(LanguageStore);

  protected displayName(t: MedicalTest): string {
    return this.lang.lang() === 'ar' ? t.nameAr : t.nameEn;
  }
}
```

- [ ] **Step 6: Write the page template**

`frontend/src/app/features/captain-panel/presentation/pages/medical-tests/medical-tests.page.html`:
```html
<div>
  <header class="mb-8">
    <h1 class="font-heading text-3xl text-ink">{{ 'medicalTests.title' | translate }}</h1>
    <p class="mt-2 text-muted">{{ 'medicalTests.description' | translate }}</p>
  </header>

  <section class="mb-8 rounded-2xl border border-border bg-card p-6 shadow-sm">
    <h2 class="font-heading text-xl text-ink">{{ 'medicalTests.add.title' | translate }}</h2>
    <form class="mt-4 grid grid-cols-1 gap-5 md:grid-cols-2 lg:grid-cols-3"
          (submit)="$event.preventDefault(); vm.submit()">
      <app-text-field [label]="'medicalTests.fields.nameEn' | translate" [placeholder]="'medicalTests.placeholders.nameEn' | translate"
        [value]="vm.nameEn()" (valueChange)="vm.nameEn.set($event)"></app-text-field>
      <app-text-field [label]="'medicalTests.fields.nameAr' | translate" [placeholder]="'medicalTests.placeholders.nameAr' | translate"
        [value]="vm.nameAr()" (valueChange)="vm.nameAr.set($event)"></app-text-field>
      <app-text-field [label]="'medicalTests.fields.unit' | translate" [placeholder]="'medicalTests.placeholders.unit' | translate"
        [value]="vm.unit()" (valueChange)="vm.unit.set($event)"></app-text-field>
      <app-text-field [label]="'medicalTests.fields.lowerBound' | translate"
        [value]="vm.lowerBound()" (valueChange)="vm.lowerBound.set($event)"></app-text-field>
      <app-text-field [label]="'medicalTests.fields.upperBound' | translate"
        [value]="vm.upperBound()" (valueChange)="vm.upperBound.set($event)"></app-text-field>
      <div class="flex items-end">
        <button type="submit" class="rounded-md bg-primary px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-50"
          [disabled]="!vm.canSubmit() || vm.submitting()">
          {{ 'medicalTests.add.submit' | translate }}
        </button>
      </div>
      <p class="md:col-span-2 lg:col-span-3 text-xs text-muted">{{ 'medicalTests.add.helper' | translate }}</p>
    </form>
  </section>

  @if (vm.loading()) {
    <p class="text-muted">{{ 'medicalTests.states.loading' | translate }}</p>
  } @else if (vm.error()) {
    <p class="text-danger">{{ 'medicalTests.states.error' | translate }}</p>
  } @else if (vm.tests().length === 0) {
    <p class="text-muted">{{ 'medicalTests.states.empty' | translate }}</p>
  } @else {
    <div class="overflow-hidden rounded-2xl border border-border bg-card shadow-sm">
      <table class="w-full text-sm">
        <thead class="border-b border-border text-start text-muted">
          <tr>
            <th class="px-4 py-3 text-start font-medium">{{ 'medicalTests.table.test' | translate }}</th>
            <th class="px-4 py-3 text-start font-medium">{{ 'medicalTests.table.unit' | translate }}</th>
            <th class="px-4 py-3 text-start font-medium">{{ 'medicalTests.table.lower' | translate }}</th>
            <th class="px-4 py-3 text-start font-medium">{{ 'medicalTests.table.upper' | translate }}</th>
            <th class="px-4 py-3"></th>
          </tr>
        </thead>
        <tbody>
          @for (t of vm.tests(); track t.id) {
            <tr class="border-b border-border/60 last:border-0">
              <td class="px-4 py-3 text-ink">{{ displayName(t) }}</td>
              <td class="px-4 py-3 text-muted">{{ t.unit }}</td>
              <td class="px-4 py-3 text-muted">{{ t.lowerBound }}</td>
              <td class="px-4 py-3 text-muted">{{ t.upperBound }}</td>
              <td class="px-4 py-3 text-end">
                <button type="button" class="text-danger hover:underline" (click)="vm.remove(t.id)">
                  {{ 'medicalTests.delete' | translate }}
                </button>
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  }
</div>
```

- [ ] **Step 7: Export the page + view model from the captain-panel barrel**

In `frontend/src/app/features/captain-panel/index.ts`, add:
```ts
export { MedicalTestsPage } from './presentation/pages/medical-tests/medical-tests.page';
export { MedicalTestsViewModel } from './presentation/pages/medical-tests/medical-tests.viewmodel';
```

- [ ] **Step 8: Run the view-model spec again + typecheck**

Run: `npx jest medical-tests.viewmodel && npx tsc -p frontend/tsconfig.app.json --noEmit`
Expected: spec PASS; no type errors. (If the repo lacks `tsconfig.app.json`, use the project's build: `cd frontend && npm run build`.)

- [ ] **Step 9: Commit**

```bash
git add frontend/src/app/features/captain-panel/presentation/pages/medical-tests frontend/src/app/features/captain-panel/index.ts frontend/src/app/features/captain-panel/testing/presentation/pages/medical-tests
git commit -m "feat(medical-tests): catalog page + view model"
```

---

## Task F4: Routing, providers, Captain-Panel card, i18n

**Files:**
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/app.config.ts`
- Modify: `frontend/src/app/features/captain-panel/presentation/pages/captain-panel/captain-panel.page.ts`
- Modify: `frontend/src/app/features/captain-panel/presentation/pages/captain-panel/captain-panel.page.html`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`
- Test: `frontend/src/app/features/captain-panel/testing/presentation/pages/captain-panel/captain-panel.page.spec.ts` (update existing)

**Interfaces:**
- Consumes: `MedicalTestsPage`, `MedicalTestsViewModel` (F3), `MEDICAL_TEST_PROVIDERS` (F1), `AuthSessionStore`, `roleGuard`, `firstLoginGuard`.
- Produces: the `captain-panel/medical-tests` route (head-coach-guarded); app-wide `MEDICAL_TEST_PROVIDERS`; a role-gated Medical Tests card on the hub; the `medicalTests` i18n namespace.

- [ ] **Step 1: Register the repository providers app-wide**

In `frontend/src/app/app.config.ts`, add the import next to the other `*_PROVIDERS` imports:
```ts
import { MEDICAL_TEST_PROVIDERS } from '@features/medical-tests/data/medical-test.providers';
```
and add `...MEDICAL_TEST_PROVIDERS,` to the `providers` array, right after `...COACH_PROVIDERS,`.

- [ ] **Step 2: Add the route**

In `frontend/src/app/app.routes.ts`:
1. Extend the captain-panel import to include the new view model:
```ts
import { RegisterSwimmerViewModel, RegisterCoachViewModel, MedicalTestsViewModel } from '@features/captain-panel';
```
2. Add this child route immediately after the `captain-panel/account-creation` route object:
```ts
      {
        path: 'captain-panel/medical-tests',
        canActivate: [firstLoginGuard, roleGuard('head_coach')],
        loadComponent: () => import('@features/captain-panel').then((m) => m.MedicalTestsPage),
        providers: [MedicalTestsViewModel],
      },
```

- [ ] **Step 3: Update the failing Captain-Panel page spec (role-gated card)**

Replace `frontend/src/app/features/captain-panel/testing/presentation/pages/captain-panel/captain-panel.page.spec.ts` with:
```ts
import { TestBed } from '@angular/core/testing';
import { CaptainPanelPage } from '@features/captain-panel/presentation/pages/captain-panel/captain-panel.page';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { provideRouter } from '@angular/router';

function build(role: 'head_coach' | 'captain') {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [CaptainPanelPage],
    providers: [provideRouter([]), { provide: AuthSessionStore, useValue: { role: () => role } }],
  });
  return TestBed.createComponent(CaptainPanelPage);
}

describe('CaptainPanelPage', () => {
  it('shows a navigating Medical Tests card for head_coach', () => {
    const fixture = build('head_coach');
    fixture.detectChanges();
    const html = (fixture.nativeElement as HTMLElement).innerHTML;
    expect(html).toContain('/captain-panel/medical-tests');
  });

  it('does not show the Medical Tests card for captain', () => {
    const fixture = build('captain');
    fixture.detectChanges();
    const html = (fixture.nativeElement as HTMLElement).innerHTML;
    expect(html).not.toContain('/captain-panel/medical-tests');
  });
});
```

- [ ] **Step 4: Run the spec to verify it fails**

Run: `npx jest captain-panel.page`
Expected: FAIL — the card is currently static (no route / not role-gated).

- [ ] **Step 5: Role-gate the cards in the page component**

Replace the body of `frontend/src/app/features/captain-panel/presentation/pages/captain-panel/captain-panel.page.ts`'s class with a computed, role-aware card list. Add imports `computed, inject` (from `@angular/core`) and `AuthSessionStore`:
```ts
import { Component, computed, inject } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  LucideUserPlus, LucideDatabase, LucideFlaskConical, LucideHeartPulse, LucideArrowRight,
} from '@lucide/angular';
import { TranslatePipe } from '@core/i18n';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type LucideIconType = any;

interface PanelCard {
  key: string;
  icon: LucideIconType;
  route?: string;
}

@Component({
  selector: 'app-captain-panel-page',
  standalone: true,
  imports: [TranslatePipe, RouterLink, NgTemplateOutlet],
  templateUrl: './captain-panel.page.html',
})
export class CaptainPanelPage {
  private readonly auth = inject(AuthSessionStore);
  protected readonly ArrowRightIcon = LucideArrowRight;

  // Medical Tests is head-coach-managed: the card is only shown to head coaches.
  protected readonly cards = computed<PanelCard[]>(() => {
    const all: PanelCard[] = [
      { key: 'accountCreation', icon: LucideUserPlus, route: '/captain-panel/account-creation' },
      { key: 'swimmerRecords', icon: LucideDatabase },
      { key: 'medicalTests', icon: LucideFlaskConical, route: '/captain-panel/medical-tests' },
      { key: 'healthMonitoring', icon: LucideHeartPulse },
    ];
    return this.auth.role() === 'head_coach' ? all : all.filter((c) => c.key !== 'medicalTests');
  });
}
```
> Note: the original component imported `LucideDynamicIcon`; the template renders icons via `[lucideIcon]` (a directive from `@lucide/angular` already available through the icon imports), so `LucideDynamicIcon` is not needed. Keep the template's `<svg [lucideIcon]="card.icon" ...>` binding unchanged.

- [ ] **Step 6: Make the template iterate the computed signal**

In `frontend/src/app/features/captain-panel/presentation/pages/captain-panel/captain-panel.page.html`, change the loop header from:
```html
    @for (card of cards; track card.key) {
```
to:
```html
    @for (card of cards(); track card.key) {
```
(Leave the rest of the template unchanged.)

- [ ] **Step 7: Add the `medicalTests` i18n namespace + refresh the card meta (English)**

In `frontend/src/app/core/i18n/en.json`, (a) change `captainPanel.cards.medicalTests.meta` from `"7 tests"` to `"Manage"`, and (b) add a top-level `medicalTests` key:
```json
"medicalTests": {
  "title": "Medical Tests",
  "description": "Define each test and its normal range. Any reading outside those bounds is flagged automatically via Health Monitoring.",
  "add": { "title": "Add a test", "helper": "Names and bounds are required; upper must exceed lower.", "submit": "Add Test" },
  "fields": { "nameEn": "Test Name (EN)", "nameAr": "Test Name (AR)", "unit": "Unit", "lowerBound": "Lower Bound", "upperBound": "Upper Bound" },
  "placeholders": { "nameEn": "e.g. Vitamin B12", "nameAr": "e.g. فيتامين ب12", "unit": "mg/dL" },
  "table": { "test": "Test", "unit": "Unit", "lower": "Lower", "upper": "Upper" },
  "states": { "loading": "Loading tests…", "empty": "No medical tests yet. Add your first test above.", "error": "Couldn't load medical tests. Please try again." },
  "delete": "Delete",
  "toasts": {
    "created": "Medical test added",
    "createFailed": "Couldn't add the medical test",
    "deleted": "Medical test deleted",
    "alreadyGone": "That test was already removed",
    "deleteFailed": "Couldn't delete the medical test"
  }
}
```

- [ ] **Step 8: Add the `medicalTests` i18n namespace + card meta (Arabic)**

In `frontend/src/app/core/i18n/ar.json`, (a) change `captainPanel.cards.medicalTests.meta` to `"إدارة"`, and (b) add:
```json
"medicalTests": {
  "title": "الفحوصات الطبية",
  "description": "حدّد كل فحص ونطاقه الطبيعي. أي قراءة خارج هذه الحدود تُوسم تلقائيًا عبر متابعة الصحة.",
  "add": { "title": "إضافة فحص", "helper": "الأسماء والحدود مطلوبة؛ يجب أن يكون الحد الأعلى أكبر من الأدنى.", "submit": "إضافة الفحص" },
  "fields": { "nameEn": "اسم الفحص (بالإنجليزية)", "nameAr": "اسم الفحص (بالعربية)", "unit": "الوحدة", "lowerBound": "الحد الأدنى", "upperBound": "الحد الأعلى" },
  "placeholders": { "nameEn": "مثال: Vitamin B12", "nameAr": "مثال: فيتامين ب12", "unit": "mg/dL" },
  "table": { "test": "الفحص", "unit": "الوحدة", "lower": "الأدنى", "upper": "الأعلى" },
  "states": { "loading": "جارٍ تحميل الفحوصات…", "empty": "لا توجد فحوصات طبية بعد. أضف أول فحص بالأعلى.", "error": "تعذّر تحميل الفحوصات الطبية. حاول مرة أخرى." },
  "delete": "حذف",
  "toasts": {
    "created": "تمت إضافة الفحص الطبي",
    "createFailed": "تعذّرت إضافة الفحص الطبي",
    "deleted": "تم حذف الفحص الطبي",
    "alreadyGone": "تم حذف هذا الفحص بالفعل",
    "deleteFailed": "تعذّر حذف الفحص الطبي"
  }
}
```

- [ ] **Step 9: Run the Captain-Panel spec + the full medical-tests suite**

Run (from `frontend/`): `npx jest captain-panel.page medical-tests`
Expected: PASS (card shown/navigates for head_coach, absent for captain; all medical-tests specs green).

- [ ] **Step 10: Build the frontend**

Run (from `frontend/`): `npm run build`
Expected: production build succeeds (route lazy-loads, i18n JSON valid).

- [ ] **Step 11: Manual smoke test**

With the backend running and logged in as **head_coach**: open Captain Panel → the Medical Tests card appears and navigates to `/captain-panel/medical-tests`; the page lists the 3 seeded tests; adding a test (EN+AR names, unit, lower<upper) prepends a row + success toast; the Add button is disabled until valid and when upper ≤ lower; deleting a row removes it. Switch language to Arabic → labels/names render RTL. Log in as **captain** → no Medical Tests card; navigating directly to `/captain-panel/medical-tests` bounces to `/`.

- [ ] **Step 12: Commit**

```bash
git add frontend/src/app/app.routes.ts frontend/src/app/app.config.ts frontend/src/app/features/captain-panel/presentation/pages/captain-panel frontend/src/app/features/captain-panel/testing/presentation/pages/captain-panel frontend/src/app/core/i18n/en.json frontend/src/app/core/i18n/ar.json
git commit -m "feat(medical-tests): route, providers, hub card (head-coach), i18n"
```

---

## Self-Review

**Spec coverage:**
- Catalog CRUD (add/list/delete), no Flagged Readings column → Tasks B5/B6 (service+API: List/Create/Delete only), F3 (table without flagged column). ✓
- New Health module (4 projects + DbContext + migration pipeline) → B1–B4, B6. ✓
- `created_by` loose Guid, no cross-module FK → B2 (entity), B3 (config: no FK), B4 (verify no FK), spec sentinel in B6 seeder. ✓
- Both names required; `name_ar` NOT NULL → B3 (config `IsRequired`), B5 (validator NameAr NotEmpty), B4 (migration column not null), F3 (form both fields), F4 (i18n both labels). ✓
- Head-coach only → B6 (`[Authorize(Roles="head_coach")]`), F4 (route `roleGuard('head_coach')` + role-gated card). ✓
- No edit; hard delete; free-text unit → reflected across B5/B6/F2/F3 (no update path). ✓
- Angular data→domain→presentation slice + captain-panel sub-page + hub card + i18n (EN/AR) → F1–F4. ✓
- Migration safety pre-check + dev-server DLL lock → B4 steps 1/3/4; Global Constraints. ✓

**Placeholder scan:** No TBD/TODO; every code step carries full source; every test step has real assertions. ✓

**Type consistency:** Backend `MedicalTest`/`CreateMedicalTestRequest`/`MedicalTestDto`/`IMedicalTestService`(`ListAsync`/`CreateAsync(req, createdBy, ct)`/`DeleteAsync`)/`IMedicalTestRepository`(`ListAsync`/`AddAsync`/`GetByIdAsync`/`RemoveAsync`/`SaveChangesAsync`) are used identically in B2/B3/B5/B6 tests and impls. Frontend `MedicalTestDtoRs`/`MedicalTest`/`IMedicalTestRepository`(`list`/`create`/`delete`)/use cases (`ListMedicalTestsUseCase.run()`, `CreateMedicalTestUseCase.run(rq)`, `DeleteMedicalTestUseCase.run(id)`)/`MedicalTestsViewModel` signals match across F1–F4. `MEDICAL_TEST_REPOSITORY`/`MEDICAL_TEST_PROVIDERS` names consistent. ✓

**Notes for the executor:**
- `LanguageStore.lang()` returns `'en' | 'ar'` (see `core/i18n/language.store.ts`); if the exported member differs, adjust `displayName()` accordingly.
- The frontend test runner is Jest via TestBed; if the repo wraps it in an npm script (e.g. `npm test -- <pattern>`), use that instead of `npx jest`.
- If `ArchitectureTests` enumerate modules explicitly, add `Kheprx.BaseBackend.Health.*` to their allow-list so the layering assertions cover the new module (spec: Backend › Testing).
</content>
