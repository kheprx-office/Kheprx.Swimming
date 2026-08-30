# .NET Backend Base (Modular Monolith) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `BaseBackend`, a reusable .NET 10 modular-monolith starter — solution + tooling, a SharedKernel, a web host with cross-cutting wiring, one sample `Catalog` module (Product CRUD on PostgreSQL via EF Core 10), architecture + unit tests, and five HTML architecture docs.

**Architecture:** Modular monolith. The host (`BaseBackend.Api`) discovers and registers self-contained modules via an `IModule` convention. Each module is four projects — `Domain`, `Application`, `Infrastructure`, `Contracts` — with its own `DbContext`. Cross-module access is only through `.Contracts`. Controllers → Service → Repository → EF Core → PostgreSQL, every response wrapped in `ApiResponse<T>`.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, FluentValidation, Mapperly (Riok.Mapperly, source-generated mapping), Serilog, Swashbuckle (Swagger), xUnit + Moq + NetArchTest. Central package management.

## Global Constraints

- **Target framework:** `net10.0` for every project, set once in `Directory.Build.props`. Never set `<TargetFramework>` in a child `.csproj`.
- **Central package management:** every package version is declared once in `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`). Individual `.csproj` files use `<PackageReference Include="X" />` with **no** `Version` attribute.
- **Package versions (starting point):** the versions in `Directory.Packages.props` (Task 1) are known-good for the .NET 10 line. If `dotnet restore` reports a newer compatible patch, update the central file and keep **all** EF Core packages (`Microsoft.EntityFrameworkCore*`, `Npgsql.EntityFrameworkCore.PostgreSQL`) on the **same** version. Do not pin versions per-project.
- **Naming:** every project is `BaseBackend.<Area>` or `BaseBackend.<Module>.<Layer>`. Root namespace matches the project name.
- **Nullable + ImplicitUsings:** enabled globally (in `Directory.Build.props`).
- **Dependency rules (enforced by Task 9 architecture tests):** `Domain` depends only on `SharedKernel`; `Application` depends on `Domain` + `SharedKernel`; `Infrastructure` depends on `Domain` + `Application` + `Contracts` + `SharedKernel`; modules reference other modules only through `.Contracts`; controllers inherit `BaseApiController`.
- **No auth** anywhere in the base.
- **Connection string key:** `ConnectionStrings:Postgres`.
- **Commit after every task** with the message shown in its final step. Run all `dotnet` commands from the repo root `C:\Users\envnt\Desktop\Base Backend` unless stated otherwise.

---

## File Structure

```
BaseBackend/
├── BaseBackend.sln
├── global.json
├── Directory.Build.props
├── Directory.Packages.props
├── .editorconfig
├── .gitignore
├── README.md
├── BaseBackend.Api/
│   ├── Controllers/BaseApiController.cs
│   ├── Controllers/ProductsController.cs
│   ├── Extensions/ServiceCollectionExtensions.cs
│   ├── Extensions/ApplicationBuilderExtensions.cs
│   ├── Filters/ValidationFilter.cs
│   ├── Middlewares/ExceptionHandlingMiddleware.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Properties/launchSettings.json
│   └── Program.cs
├── src/
│   ├── SharedKernel/BaseBackend.SharedKernel/
│   │   ├── Responses/BaseApiResponse.cs
│   │   ├── Responses/ApiResponse.cs
│   │   ├── Domain/BaseEntity.cs
│   │   ├── Domain/DomainException.cs
│   │   └── Modules/IModule.cs
│   │   └── Modules/ModuleRegistration.cs
│   └── Modules/Catalog/
│       ├── BaseBackend.Catalog.Domain/
│       │   ├── Entities/Product.cs
│       │   └── Exceptions/InvalidProductException.cs
│       ├── BaseBackend.Catalog.Contracts/ICatalogModule.cs
│       ├── BaseBackend.Catalog.Application/
│       │   ├── DTOs/ProductDtos.cs
│       │   ├── Validators/ProductRequestValidators.cs
│       │   ├── Mappings/CatalogMapper.cs
│       │   └── Services/Interfaces/IProductService.cs
│       └── BaseBackend.Catalog.Infrastructure/
│           ├── Data/CatalogDbContext.cs
│           ├── Data/CatalogDbContextFactory.cs
│           ├── Configurations/ProductConfiguration.cs
│           ├── Repositories/IProductRepository.cs
│           ├── Repositories/ProductRepository.cs
│           ├── Services/ProductService.cs
│           ├── Services/CatalogModuleApi.cs
│           ├── CatalogModule.cs
│           └── Migrations/            (generated in Task 8)
├── tests/
│   ├── BaseBackend.Catalog.UnitTests/
│   └── BaseBackend.ArchitectureTests/
└── docs/
    ├── ARCHITECTURE_OVERVIEW.html
    ├── SOLUTION_STRUCTURE.html
    ├── PROJECT_LIBRARIES.html
    ├── REQUEST_FLOW.html
    ├── MODULE_ANATOMY.html
    └── superpowers/{specs,plans}/   (already present)
```

---

### Task 1: Solution skeleton & repository tooling

**Files:**
- Create: `BaseBackend.sln`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `README.md`

**Interfaces:**
- Produces: the solution and central MSBuild props every later task plugs into. Package IDs/versions referenced everywhere come from `Directory.Packages.props`.

- [ ] **Step 1: Create the solution**

Run from repo root:
```bash
dotnet new sln -n BaseBackend
```
Expected: `The template "Solution File" was created successfully.`

- [ ] **Step 2: Create `global.json`** (pins the SDK to the installed 10.0 line)

```json
{
  "sdk": {
    "version": "10.0.102",
    "rollForward": "latestFeature"
  }
}
```

- [ ] **Step 3: Create `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Create `Directory.Packages.props`** (central versions — see Global Constraints about bumping)

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Runtime -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
    <!-- Security pin: patched transitive (Npgsql pulls a vulnerable 9.0.0; CVE-2026-26171). Keep >= 10.0.6. -->
    <PackageVersion Include="System.Security.Cryptography.Xml" Version="10.0.8" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.0" />
    <PackageVersion Include="FluentValidation" Version="11.11.0" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.11.0" />
    <PackageVersion Include="Riok.Mapperly" Version="4.3.1" />
    <PackageVersion Include="Serilog.AspNetCore" Version="9.0.0" />
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="7.2.0" />
    <!-- Test -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="NetArchTest.Rules" Version="1.3.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Create `.gitignore`**

```gitignore
## .NET
bin/
obj/
[Dd]ebug/
[Rr]elease/
publish/
publish-dev/
publish-prod/
*.user
.vs/
artifacts/
TestResults/
*.log
```

- [ ] **Step 6: Create `.editorconfig`**

```ini
root = true

[*]
charset = utf-8
indent_style = space
insert_final_newline = true
trim_trailing_whitespace = true

[*.cs]
indent_size = 4
dotnet_sort_system_directives_first = true
csharp_new_line_before_open_brace = all
dotnet_style_namespace_match_folder = true
csharp_style_namespace_declarations = file_scoped:warning

[*.{json,csproj,props,html}]
indent_size = 2
```

- [ ] **Step 7: Create `README.md`**

```markdown
# BaseBackend

A reusable .NET 10 modular-monolith backend base. See `docs/ARCHITECTURE_OVERVIEW.html` to start.

## Run
```bash
dotnet run --project BaseBackend.Api
```
Swagger UI: `https://localhost:<port>/swagger` (Development only).

## Add a module
1. Create four projects under `src/Modules/<Name>/`: `.Domain`, `.Application`, `.Infrastructure`, `.Contracts`.
2. In `.Infrastructure`, add a `class <Name>Module : IModule` that registers the module's DbContext and services.
3. Reference the new `.Infrastructure` project from `BaseBackend.Api`. No `Program.cs` edits — modules are auto-discovered.

## Migrations
```bash
dotnet ef migrations add <Name> \
  --project src/Modules/<Name>/BaseBackend.<Name>.Infrastructure \
  --startup-project BaseBackend.Api \
  --context <Name>DbContext --output-dir Migrations
dotnet ef database update --project src/Modules/<Name>/BaseBackend.<Name>.Infrastructure \
  --startup-project BaseBackend.Api --context <Name>DbContext
```

## Database
PostgreSQL. Set `ConnectionStrings:Postgres` in `BaseBackend.Api/appsettings.json`.
```

- [ ] **Step 8: Verify the solution builds**

Run: `dotnet build BaseBackend.sln`
Expected: `Build succeeded.` with 0 projects compiled (empty solution restores/builds clean).

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "build: solution skeleton and repository tooling"
```

---

### Task 2: SharedKernel

**Files:**
- Create: `src/SharedKernel/BaseBackend.SharedKernel/BaseBackend.SharedKernel.csproj`
- Create: `src/SharedKernel/BaseBackend.SharedKernel/Responses/BaseApiResponse.cs`, `Responses/ApiResponse.cs`, `Domain/BaseEntity.cs`, `Domain/DomainException.cs`, `Modules/IModule.cs`, `Modules/ModuleRegistration.cs`

**Interfaces:**
- Produces:
  - `ApiResponse<T>.Success(string message, T? data)` and `ApiResponse<T>.Failure(string message, string error)`; base props `SuccessStatus` (bool), `Message` (string), `Error` (string?), `Data` (T?).
  - `abstract class BaseEntity` with `int Id`, `DateTime CreatedAt`, `DateTime? UpdatedAt`, `protected void Touch()`.
  - `abstract class DomainException : Exception`.
  - `interface IModule { IServiceCollection Register(IServiceCollection services, IConfiguration configuration); }`.
  - `IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration)` — discovers every `IModule` in `BaseBackend.*.dll` and calls `Register`.

- [ ] **Step 1: Create the project and add it to the solution**

```bash
dotnet new classlib -n BaseBackend.SharedKernel -o src/SharedKernel/BaseBackend.SharedKernel
rm src/SharedKernel/BaseBackend.SharedKernel/Class1.cs
dotnet sln add src/SharedKernel/BaseBackend.SharedKernel/BaseBackend.SharedKernel.csproj --solution-folder src/SharedKernel
```
Expected: project created and `Project ... added to the solution.`

- [ ] **Step 2: Set the project references (DI + Configuration abstractions)**

Replace `src/SharedKernel/BaseBackend.SharedKernel/BaseBackend.SharedKernel.csproj` contents:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Write `Responses/BaseApiResponse.cs`**

```csharp
namespace BaseBackend.SharedKernel.Responses;

public abstract class BaseApiResponse
{
    public bool SuccessStatus { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Error { get; init; }
}
```

- [ ] **Step 4: Write `Responses/ApiResponse.cs`**

```csharp
namespace BaseBackend.SharedKernel.Responses;

public sealed class ApiResponse<T> : BaseApiResponse
{
    public T? Data { get; init; }

    public static ApiResponse<T> Success(string message, T? data) =>
        new() { SuccessStatus = true, Message = message, Data = data };

    public static ApiResponse<T> Failure(string message, string error) =>
        new() { SuccessStatus = false, Message = message, Error = error };
}
```

- [ ] **Step 5: Write `Domain/BaseEntity.cs`**

```csharp
namespace BaseBackend.SharedKernel.Domain;

public abstract class BaseEntity
{
    public int Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    protected void Touch() => UpdatedAt = DateTime.UtcNow;
}
```

- [ ] **Step 6: Write `Domain/DomainException.cs`**

```csharp
namespace BaseBackend.SharedKernel.Domain;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
```

- [ ] **Step 7: Write `Modules/IModule.cs`**

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BaseBackend.SharedKernel.Modules;

public interface IModule
{
    IServiceCollection Register(IServiceCollection services, IConfiguration configuration);
}
```

- [ ] **Step 8: Write `Modules/ModuleRegistration.cs`**

```csharp
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BaseBackend.SharedKernel.Modules;

public static class ModuleRegistration
{
    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration)
    {
        foreach (var module in DiscoverModules())
            module.Register(services, configuration);
        return services;
    }

    private static IEnumerable<IModule> DiscoverModules()
    {
        var loaded = new HashSet<string>(
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name!));

        foreach (var dll in Directory.GetFiles(AppContext.BaseDirectory, "BaseBackend.*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(dll);
            if (loaded.Add(name))
            {
                try { Assembly.LoadFrom(dll); }
                catch { /* skip assemblies that cannot be loaded */ }
            }
        }

        var moduleType = typeof(IModule);
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(t => moduleType.IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
            .Select(t => (IModule)Activator.CreateInstance(t)!);
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }
}
```

- [ ] **Step 9: Build**

Run: `dotnet build src/SharedKernel/BaseBackend.SharedKernel/BaseBackend.SharedKernel.csproj`
Expected: `Build succeeded.` (Behavior is exercised by Task 3's `ApiResponse` tests and Task 9's module-discovery test.)

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat: add SharedKernel (ApiResponse, BaseEntity, IModule, AddModules)"
```

---

### Task 3: Catalog.Domain + test project + domain/ApiResponse tests

**Files:**
- Create: `src/Modules/Catalog/BaseBackend.Catalog.Domain/BaseBackend.Catalog.Domain.csproj`, `Entities/Product.cs`, `Exceptions/InvalidProductException.cs`
- Create: `tests/BaseBackend.Catalog.UnitTests/BaseBackend.Catalog.UnitTests.csproj`, `ProductTests.cs`, `ApiResponseTests.cs`

**Interfaces:**
- Produces:
  - `sealed class Product : BaseEntity` with `Name` (string), `Description` (string?), `Price` (decimal); ctor `Product(string name, decimal price, string? description = null)`; methods `Rename(string)`, `ChangePrice(decimal)`, `UpdateDescription(string?)`.
  - `sealed class InvalidProductException : DomainException`.

- [ ] **Step 1: Create the Domain project**

```bash
dotnet new classlib -n BaseBackend.Catalog.Domain -o src/Modules/Catalog/BaseBackend.Catalog.Domain
rm src/Modules/Catalog/BaseBackend.Catalog.Domain/Class1.cs
dotnet sln add src/Modules/Catalog/BaseBackend.Catalog.Domain/BaseBackend.Catalog.Domain.csproj --solution-folder src/Modules/Catalog
dotnet add src/Modules/Catalog/BaseBackend.Catalog.Domain/BaseBackend.Catalog.Domain.csproj reference src/SharedKernel/BaseBackend.SharedKernel/BaseBackend.SharedKernel.csproj
```

- [ ] **Step 2: Write `Exceptions/InvalidProductException.cs`**

```csharp
using BaseBackend.SharedKernel.Domain;

namespace BaseBackend.Catalog.Domain.Exceptions;

public sealed class InvalidProductException : DomainException
{
    public InvalidProductException(string message) : base(message) { }
}
```

- [ ] **Step 3: Write `Entities/Product.cs`**

```csharp
using BaseBackend.Catalog.Domain.Exceptions;
using BaseBackend.SharedKernel.Domain;

namespace BaseBackend.Catalog.Domain.Entities;

public sealed class Product : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }

    private Product() { } // EF Core

    public Product(string name, decimal price, string? description = null)
    {
        Rename(name);
        ChangePrice(price);
        UpdateDescription(description);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidProductException("Product name is required.");
        Name = name.Trim();
        Touch();
    }

    public void ChangePrice(decimal price)
    {
        if (price <= 0)
            throw new InvalidProductException("Product price must be greater than zero.");
        Price = price;
        Touch();
    }

    public void UpdateDescription(string? description)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Touch();
    }
}
```

- [ ] **Step 4: Create the test project**

```bash
dotnet new xunit -n BaseBackend.Catalog.UnitTests -o tests/BaseBackend.Catalog.UnitTests
rm tests/BaseBackend.Catalog.UnitTests/UnitTest1.cs
dotnet sln add tests/BaseBackend.Catalog.UnitTests/BaseBackend.Catalog.UnitTests.csproj --solution-folder tests
dotnet add tests/BaseBackend.Catalog.UnitTests/BaseBackend.Catalog.UnitTests.csproj reference src/Modules/Catalog/BaseBackend.Catalog.Domain/BaseBackend.Catalog.Domain.csproj
dotnet add tests/BaseBackend.Catalog.UnitTests/BaseBackend.Catalog.UnitTests.csproj reference src/SharedKernel/BaseBackend.SharedKernel/BaseBackend.SharedKernel.csproj
```

Then set `tests/BaseBackend.Catalog.UnitTests/BaseBackend.Catalog.UnitTests.csproj` package refs (versionless — CPM supplies versions; `dotnet new xunit` may have written versions, remove them):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Moq" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Modules\Catalog\BaseBackend.Catalog.Domain\BaseBackend.Catalog.Domain.csproj" />
    <ProjectReference Include="..\..\src\SharedKernel\BaseBackend.SharedKernel\BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Write the failing tests `ProductTests.cs`**

```csharp
using BaseBackend.Catalog.Domain.Entities;
using BaseBackend.Catalog.Domain.Exceptions;
using Xunit;

namespace BaseBackend.Catalog.UnitTests;

public class ProductTests
{
    [Fact]
    public void Constructor_sets_properties()
    {
        var product = new Product("Widget", 9.99m, "A widget");

        Assert.Equal("Widget", product.Name);
        Assert.Equal(9.99m, product.Price);
        Assert.Equal("A widget", product.Description);
    }

    [Fact]
    public void Constructor_with_zero_price_throws()
        => Assert.Throws<InvalidProductException>(() => new Product("Widget", 0m));

    [Fact]
    public void Rename_with_blank_throws()
    {
        var product = new Product("Widget", 1m);
        Assert.Throws<InvalidProductException>(() => product.Rename("  "));
    }

    [Fact]
    public void ChangePrice_updates_value_and_touches()
    {
        var product = new Product("Widget", 1m);
        product.ChangePrice(5m);
        Assert.Equal(5m, product.Price);
        Assert.NotNull(product.UpdatedAt);
    }
}
```

- [ ] **Step 6: Write `ApiResponseTests.cs`**

```csharp
using BaseBackend.SharedKernel.Responses;
using Xunit;

namespace BaseBackend.Catalog.UnitTests;

public class ApiResponseTests
{
    [Fact]
    public void Success_sets_status_message_and_data()
    {
        var response = ApiResponse<int>.Success("ok", 5);
        Assert.True(response.SuccessStatus);
        Assert.Equal("ok", response.Message);
        Assert.Equal(5, response.Data);
        Assert.Null(response.Error);
    }

    [Fact]
    public void Failure_sets_status_false_and_error()
    {
        var response = ApiResponse<int>.Failure("bad", "CODE");
        Assert.False(response.SuccessStatus);
        Assert.Equal("CODE", response.Error);
    }
}
```

- [ ] **Step 7: Run the tests — verify they pass**

Run: `dotnet test tests/BaseBackend.Catalog.UnitTests/BaseBackend.Catalog.UnitTests.csproj`
Expected: PASS, 6 tests passed. (Implementation is written; this confirms the project wires up.)

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: add Catalog.Domain (Product) with domain unit tests"
```

---

### Task 4: Catalog.Contracts + Catalog.Application

**Files:**
- Create: `src/Modules/Catalog/BaseBackend.Catalog.Contracts/BaseBackend.Catalog.Contracts.csproj`, `ICatalogModule.cs`
- Create: `src/Modules/Catalog/BaseBackend.Catalog.Application/BaseBackend.Catalog.Application.csproj`, `DTOs/ProductDtos.cs`, `Validators/ProductRequestValidators.cs`, `Mappings/CatalogMapper.cs`, `Services/Interfaces/IProductService.cs`
- Create (tests): `tests/BaseBackend.Catalog.UnitTests/CreateProductRequestValidatorTests.cs`, `CatalogMapperTests.cs`

**Interfaces:**
- Consumes: `Product` (Task 3), `BaseEntity` (Task 2).
- Produces:
  - `record ProductDto(int Id, string Name, string? Description, decimal Price, DateTime CreatedAt, DateTime? UpdatedAt)`
  - `record CreateProductRequest(string Name, decimal Price, string? Description)`
  - `record UpdateProductRequest(string Name, decimal Price, string? Description)`
  - `interface IProductService` with `GetAllAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync` (signatures below)
  - `[Mapper] partial class CatalogMapper` (Mapperly) with `ProductDto ToDto(Product)` and `IReadOnlyList<ProductDto> ToDtoList(IReadOnlyList<Product>)`
  - `interface ICatalogModule { Task<bool> ProductExistsAsync(int productId, CancellationToken ct = default); }`

- [ ] **Step 1: Create the Contracts project**

```bash
dotnet new classlib -n BaseBackend.Catalog.Contracts -o src/Modules/Catalog/BaseBackend.Catalog.Contracts
rm src/Modules/Catalog/BaseBackend.Catalog.Contracts/Class1.cs
dotnet sln add src/Modules/Catalog/BaseBackend.Catalog.Contracts/BaseBackend.Catalog.Contracts.csproj --solution-folder src/Modules/Catalog
```

- [ ] **Step 2: Write `Contracts/ICatalogModule.cs`**

```csharp
namespace BaseBackend.Catalog.Contracts;

public interface ICatalogModule
{
    Task<bool> ProductExistsAsync(int productId, CancellationToken ct = default);
}
```

- [ ] **Step 3: Create the Application project and its references**

```bash
dotnet new classlib -n BaseBackend.Catalog.Application -o src/Modules/Catalog/BaseBackend.Catalog.Application
rm src/Modules/Catalog/BaseBackend.Catalog.Application/Class1.cs
dotnet sln add src/Modules/Catalog/BaseBackend.Catalog.Application/BaseBackend.Catalog.Application.csproj --solution-folder src/Modules/Catalog
dotnet add src/Modules/Catalog/BaseBackend.Catalog.Application/BaseBackend.Catalog.Application.csproj reference src/Modules/Catalog/BaseBackend.Catalog.Domain/BaseBackend.Catalog.Domain.csproj
dotnet add src/Modules/Catalog/BaseBackend.Catalog.Application/BaseBackend.Catalog.Application.csproj reference src/SharedKernel/BaseBackend.SharedKernel/BaseBackend.SharedKernel.csproj
```

Set `BaseBackend.Catalog.Application.csproj` package refs:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="Riok.Mapperly" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\BaseBackend.Catalog.Domain\BaseBackend.Catalog.Domain.csproj" />
    <ProjectReference Include="..\..\..\SharedKernel\BaseBackend.SharedKernel\BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Write `DTOs/ProductDtos.cs`**

```csharp
namespace BaseBackend.Catalog.Application.DTOs;

public sealed record ProductDto(
    int Id, string Name, string? Description, decimal Price, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateProductRequest(string Name, decimal Price, string? Description);

public sealed record UpdateProductRequest(string Name, decimal Price, string? Description);
```

- [ ] **Step 5: Write `Validators/ProductRequestValidators.cs`**

```csharp
using BaseBackend.Catalog.Application.DTOs;
using FluentValidation;

namespace BaseBackend.Catalog.Application.Validators;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
```

- [ ] **Step 6: Write `Mappings/CatalogMapper.cs`** (Mapperly source-generated mapper)

```csharp
using BaseBackend.Catalog.Application.DTOs;
using BaseBackend.Catalog.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace BaseBackend.Catalog.Application.Mappings;

[Mapper]
public partial class CatalogMapper
{
    public partial ProductDto ToDto(Product product);
    public partial IReadOnlyList<ProductDto> ToDtoList(IReadOnlyList<Product> products);
}
```

Mapperly generates the mapping at compile time by matching `Product`'s properties to `ProductDto`'s record parameters (all names align: Id, Name, Description, Price, CreatedAt, UpdatedAt). No runtime dependency, no reflection. Because `TreatWarningsAsErrors=true`, any Mapperly diagnostic (e.g. an unmapped member) fails the build — that is the desired compile-time safety net.

- [ ] **Step 7: Write `Services/Interfaces/IProductService.cs`**

```csharp
using BaseBackend.Catalog.Application.DTOs;

namespace BaseBackend.Catalog.Application.Services.Interfaces;

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default);
    Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
    Task<ProductDto?> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
```

- [ ] **Step 8: Add the Application reference to the test project**

```bash
dotnet add tests/BaseBackend.Catalog.UnitTests/BaseBackend.Catalog.UnitTests.csproj reference src/Modules/Catalog/BaseBackend.Catalog.Application/BaseBackend.Catalog.Application.csproj
```

- [ ] **Step 9: Write the failing tests `CreateProductRequestValidatorTests.cs`**

```csharp
using BaseBackend.Catalog.Application.DTOs;
using BaseBackend.Catalog.Application.Validators;
using Xunit;

namespace BaseBackend.Catalog.UnitTests;

public class CreateProductRequestValidatorTests
{
    private readonly CreateProductRequestValidator _validator = new();

    [Fact]
    public void Empty_name_is_invalid()
    {
        var result = _validator.Validate(new CreateProductRequest("", 5m, null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Zero_price_is_invalid()
    {
        var result = _validator.Validate(new CreateProductRequest("Widget", 0m, null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.Validate(new CreateProductRequest("Widget", 5m, "ok"));
        Assert.True(result.IsValid);
    }
}
```

- [ ] **Step 10: Write `CatalogMapperTests.cs`** (verifies the Mapperly mapping copies every field)

```csharp
using BaseBackend.Catalog.Application.Mappings;
using BaseBackend.Catalog.Domain.Entities;
using Xunit;

namespace BaseBackend.Catalog.UnitTests;

public class CatalogMapperTests
{
    private readonly CatalogMapper _mapper = new();

    [Fact]
    public void ToDto_maps_all_fields()
    {
        var product = new Product("Widget", 9.99m, "A widget");

        var dto = _mapper.ToDto(product);

        Assert.Equal(product.Id, dto.Id);
        Assert.Equal("Widget", dto.Name);
        Assert.Equal("A widget", dto.Description);
        Assert.Equal(9.99m, dto.Price);
        Assert.Equal(product.CreatedAt, dto.CreatedAt);
    }

    [Fact]
    public void ToDtoList_maps_each_element()
    {
        var products = new List<Product> { new("A", 1m), new("B", 2m) };

        var dtos = _mapper.ToDtoList(products);

        Assert.Equal(2, dtos.Count);
        Assert.Equal("A", dtos[0].Name);
        Assert.Equal("B", dtos[1].Name);
    }
}
```

- [ ] **Step 11: Run the tests — verify they pass**

Run: `dotnet test tests/BaseBackend.Catalog.UnitTests/BaseBackend.Catalog.UnitTests.csproj`
Expected: PASS, 11 tests passed (6 prior + 3 validator + 2 mapper).

> Note: `Riok.Mapperly` is a source generator — it produces the `CatalogMapper` partial method bodies at build time with no runtime dependency. If the build reports a Mapperly diagnostic (RMGxxx) about an unmapped member, that is a real signal: fix the mapping declaration, do not suppress it. Use the latest stable `Riok.Mapperly` that restores on net10.0 and record that version in `Directory.Packages.props`.

- [ ] **Step 12: Commit**

```bash
git add -A
git commit -m "feat: add Catalog.Contracts and Catalog.Application (DTOs, validators, mapping) with tests"
```

---

### Task 5: Catalog.Infrastructure (DbContext, repository, service, module registration)

**Files:**
- Create: `src/Modules/Catalog/BaseBackend.Catalog.Infrastructure/BaseBackend.Catalog.Infrastructure.csproj`, `Data/CatalogDbContext.cs`, `Data/CatalogDbContextFactory.cs`, `Configurations/ProductConfiguration.cs`, `Repositories/IProductRepository.cs`, `Repositories/ProductRepository.cs`, `Services/ProductService.cs`, `Services/CatalogModuleApi.cs`, `CatalogModule.cs`
- Create (tests): `tests/BaseBackend.Catalog.UnitTests/ProductServiceTests.cs`

**Interfaces:**
- Consumes: `Product`, `IProductService`, `ProductDto`, `CreateProductRequest`, `UpdateProductRequest`, `CatalogMapper`, `ICatalogModule`, `IModule`.
- Produces:
  - `interface IProductRepository` with `GetAllAsync`, `GetByIdAsync`, `AddAsync`, `Remove`, `ExistsAsync`, `SaveChangesAsync` (signatures below).
  - `sealed class CatalogDbContext : DbContext` exposing `DbSet<Product> Products`.
  - `sealed class CatalogModule : IModule` registering `CatalogDbContext`, `IProductRepository`, `IProductService`, `ICatalogModule`.

- [ ] **Step 1: Create the Infrastructure project and references**

```bash
dotnet new classlib -n BaseBackend.Catalog.Infrastructure -o src/Modules/Catalog/BaseBackend.Catalog.Infrastructure
rm src/Modules/Catalog/BaseBackend.Catalog.Infrastructure/Class1.cs
dotnet sln add src/Modules/Catalog/BaseBackend.Catalog.Infrastructure/BaseBackend.Catalog.Infrastructure.csproj --solution-folder src/Modules/Catalog
```

Set `BaseBackend.Catalog.Infrastructure.csproj` (references + EF + `InternalsVisibleTo` for tests):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <!-- Pin a patched version to override the vulnerable transitive (CVE-2026-26171) pulled by Npgsql. -->
    <PackageReference Include="System.Security.Cryptography.Xml" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\BaseBackend.Catalog.Domain\BaseBackend.Catalog.Domain.csproj" />
    <ProjectReference Include="..\BaseBackend.Catalog.Application\BaseBackend.Catalog.Application.csproj" />
    <ProjectReference Include="..\BaseBackend.Catalog.Contracts\BaseBackend.Catalog.Contracts.csproj" />
    <ProjectReference Include="..\..\..\SharedKernel\BaseBackend.SharedKernel\BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="BaseBackend.Catalog.UnitTests" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write `Data/CatalogDbContext.cs`**

```csharp
using BaseBackend.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BaseBackend.Catalog.Infrastructure.Data;

public sealed class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Step 3: Write `Configurations/ProductConfiguration.cs`**

```csharp
using BaseBackend.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BaseBackend.Catalog.Infrastructure.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Price).HasColumnType("numeric(18,2)");
        builder.Property(p => p.CreatedAt).IsRequired();
    }
}
```

- [ ] **Step 4: Write `Repositories/IProductRepository.cs`**

```csharp
using BaseBackend.Catalog.Domain.Entities;

namespace BaseBackend.Catalog.Infrastructure.Repositories;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default);
    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    void Remove(Product product);
    Task<bool> ExistsAsync(int id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 5: Write `Repositories/ProductRepository.cs`**

```csharp
using BaseBackend.Catalog.Domain.Entities;
using BaseBackend.Catalog.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BaseBackend.Catalog.Infrastructure.Repositories;

internal sealed class ProductRepository : IProductRepository
{
    private readonly CatalogDbContext _db;

    public ProductRepository(CatalogDbContext db) => _db = db;

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
        => await _db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct);

    public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(Product product, CancellationToken ct = default)
        => await _db.Products.AddAsync(product, ct);

    public void Remove(Product product) => _db.Products.Remove(product);

    public Task<bool> ExistsAsync(int id, CancellationToken ct = default)
        => _db.Products.AnyAsync(p => p.Id == id, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
```

- [ ] **Step 6: Write `Services/ProductService.cs`**

```csharp
using BaseBackend.Catalog.Application.DTOs;
using BaseBackend.Catalog.Application.Mappings;
using BaseBackend.Catalog.Application.Services.Interfaces;
using BaseBackend.Catalog.Domain.Entities;
using BaseBackend.Catalog.Infrastructure.Repositories;

namespace BaseBackend.Catalog.Infrastructure.Services;

internal sealed class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly CatalogMapper _mapper;

    public ProductService(IProductRepository repository, CatalogMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default)
        => _mapper.ToDtoList(await _repository.GetAllAsync(ct));

    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var product = await _repository.GetByIdAsync(id, ct);
        return product is null ? null : _mapper.ToDto(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var product = new Product(request.Name, request.Price, request.Description);
        await _repository.AddAsync(product, ct);
        await _repository.SaveChangesAsync(ct);
        return _mapper.ToDto(product);
    }

    public async Task<ProductDto?> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _repository.GetByIdAsync(id, ct);
        if (product is null) return null;

        product.Rename(request.Name);
        product.ChangePrice(request.Price);
        product.UpdateDescription(request.Description);
        await _repository.SaveChangesAsync(ct);
        return _mapper.ToDto(product);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await _repository.GetByIdAsync(id, ct);
        if (product is null) return false;

        _repository.Remove(product);
        await _repository.SaveChangesAsync(ct);
        return true;
    }
}
```

- [ ] **Step 7: Write `Services/CatalogModuleApi.cs`** (the cross-module facade implementing `ICatalogModule`)

```csharp
using BaseBackend.Catalog.Contracts;
using BaseBackend.Catalog.Infrastructure.Repositories;

namespace BaseBackend.Catalog.Infrastructure.Services;

internal sealed class CatalogModuleApi : ICatalogModule
{
    private readonly IProductRepository _repository;

    public CatalogModuleApi(IProductRepository repository) => _repository = repository;

    public Task<bool> ProductExistsAsync(int productId, CancellationToken ct = default)
        => _repository.ExistsAsync(productId, ct);
}
```

- [ ] **Step 8: Write `Data/CatalogDbContextFactory.cs`** (design-time factory so `dotnet ef` works without the host)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BaseBackend.Catalog.Infrastructure.Data;

public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=basebackend;Username=postgres;Password=postgres")
            .Options;
        return new CatalogDbContext(options);
    }
}
```

- [ ] **Step 9: Write `CatalogModule.cs`** (DI registration via `IModule`)

```csharp
using BaseBackend.Catalog.Application.Mappings;
using BaseBackend.Catalog.Application.Services.Interfaces;
using BaseBackend.Catalog.Contracts;
using BaseBackend.Catalog.Infrastructure.Data;
using BaseBackend.Catalog.Infrastructure.Repositories;
using BaseBackend.Catalog.Infrastructure.Services;
using BaseBackend.SharedKernel.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BaseBackend.Catalog.Infrastructure;

public sealed class CatalogModule : IModule
{
    public IServiceCollection Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddSingleton<CatalogMapper>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICatalogModule, CatalogModuleApi>();
        return services;
    }
}
```

(The Mapperly `CatalogMapper` is stateless, so a singleton is appropriate. Each module registers its own mapper here — there is no global mapper scan.)

- [ ] **Step 10: Write the failing tests `ProductServiceTests.cs`** (repository mocked with Moq)

```csharp
using BaseBackend.Catalog.Application.DTOs;
using BaseBackend.Catalog.Application.Mappings;
using BaseBackend.Catalog.Domain.Entities;
using BaseBackend.Catalog.Infrastructure.Repositories;
using BaseBackend.Catalog.Infrastructure.Services;
using Moq;
using Xunit;

namespace BaseBackend.Catalog.UnitTests;

public class ProductServiceTests
{
    private readonly CatalogMapper _mapper = new();

    [Fact]
    public async Task GetByIdAsync_returns_null_when_missing()
    {
        var repo = new Mock<IProductRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Product?)null);

        var service = new ProductService(repo.Object, _mapper);

        Assert.Null(await service.GetByIdAsync(1));
    }

    [Fact]
    public async Task CreateAsync_persists_and_returns_dto()
    {
        var repo = new Mock<IProductRepository>();
        var service = new ProductService(repo.Object, _mapper);

        var dto = await service.CreateAsync(new CreateProductRequest("Widget", 9.99m, null));

        Assert.Equal("Widget", dto.Name);
        Assert.Equal(9.99m, dto.Price);
        repo.Verify(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_returns_false_when_missing()
    {
        var repo = new Mock<IProductRepository>();
        repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((Product?)null);

        var service = new ProductService(repo.Object, _mapper);

        Assert.False(await service.DeleteAsync(7));
    }
}
```

- [ ] **Step 11: Run the tests — verify they pass**

Run: `dotnet test tests/BaseBackend.Catalog.UnitTests/BaseBackend.Catalog.UnitTests.csproj`
Expected: PASS, 13 tests passed.

- [ ] **Step 12: Commit**

```bash
git add -A
git commit -m "feat: add Catalog.Infrastructure (DbContext, repository, service, module registration) with tests"
```

---

### Task 6: Host bootstrap & shared infrastructure wiring

**Files:**
- Create: `BaseBackend.Api/BaseBackend.Api.csproj`, `Program.cs`, `Controllers/BaseApiController.cs`, `Extensions/ServiceCollectionExtensions.cs`, `Extensions/ApplicationBuilderExtensions.cs`, `Filters/ValidationFilter.cs`, `Middlewares/ExceptionHandlingMiddleware.cs`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`

**Interfaces:**
- Consumes: `AddModules` (Task 2), `ApiResponse<T>` (Task 2), `CatalogModule` via project reference (Task 5).
- Produces:
  - `IServiceCollection AddSharedInfrastructure(this IServiceCollection, IConfiguration)`
  - `WebApplication UseSharedPipeline(this WebApplication, IWebHostEnvironment)`
  - `abstract class BaseApiController : ControllerBase` (route `api/[controller]`)

- [ ] **Step 1: Create the Web API host and wire references**

```bash
dotnet new webapi -n BaseBackend.Api -o BaseBackend.Api --use-controllers
rm -f BaseBackend.Api/WeatherForecast.cs BaseBackend.Api/Controllers/WeatherForecastController.cs BaseBackend.Api/BaseBackend.Api.http
dotnet sln add BaseBackend.Api/BaseBackend.Api.csproj --solution-folder host
dotnet add BaseBackend.Api/BaseBackend.Api.csproj reference src/Modules/Catalog/BaseBackend.Catalog.Infrastructure/BaseBackend.Catalog.Infrastructure.csproj
```

- [ ] **Step 2: Set `BaseBackend.Api.csproj`** (versionless package refs; module reference flows transitively)

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <PackageReference Include="Swashbuckle.AspNetCore" />
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\src\Modules\Catalog\BaseBackend.Catalog.Infrastructure\BaseBackend.Catalog.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Write `Controllers/BaseApiController.cs`**

```csharp
using Microsoft.AspNetCore.Mvc;

namespace BaseBackend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected string AcceptLanguage => Request.Headers.AcceptLanguage.ToString();
}
```

- [ ] **Step 4: Write `Filters/ValidationFilter.cs`** (runs any registered FluentValidation validator for action arguments)

```csharp
using BaseBackend.SharedKernel.Responses;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaseBackend.Api.Filters;

public sealed class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;

    public ValidationFilter(IServiceProvider services) => _services = services;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_services.GetService(validatorType) is IValidator validator)
            {
                var result = await validator.ValidateAsync(new ValidationContext<object>(argument));
                if (!result.IsValid)
                {
                    var errors = string.Join("; ", result.Errors.Select(e => e.ErrorMessage));
                    context.Result = new BadRequestObjectResult(
                        ApiResponse<object>.Failure("Validation failed", errors));
                    return;
                }
            }
        }

        await next();
    }
}
```

- [ ] **Step 5: Write `Middlewares/ExceptionHandlingMiddleware.cs`**

```csharp
using BaseBackend.SharedKernel.Domain;
using BaseBackend.SharedKernel.Responses;

namespace BaseBackend.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain rule violated");
            await WriteAsync(context, StatusCodes.Status400BadRequest, ex.Message, "DOMAIN_RULE_VIOLATION");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteAsync(context, StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.", "SERVER_ERROR");
        }
    }

    private static Task WriteAsync(HttpContext context, int statusCode, string message, string error)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(ApiResponse<object>.Failure(message, error));
    }
}
```

- [ ] **Step 6: Write `Extensions/ServiceCollectionExtensions.cs`**

```csharp
using System.Reflection;
using System.Text.Json;
using BaseBackend.Api.Filters;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BaseBackend.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var moduleAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("BaseBackend.", StringComparison.Ordinal) == true)
            .ToArray();

        services.AddControllers(options => options.Filters.Add<ValidationFilter>())
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddValidatorsFromAssemblies(moduleAssemblies, includeInternalTypes: true);

        services.AddCors(options => options.AddPolicy("AllowAll",
            policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

        return services;
    }
}
```

> Mapping is handled by Mapperly: each module registers its own source-generated mapper (e.g. `CatalogModule` registers `CatalogMapper` as a singleton). There is no global mapper scan in the host — FluentValidation validators are still scanned here because they are discovered by type, whereas each Mapperly mapper is a concrete class owned by its module.

- [ ] **Step 7: Write `Extensions/ApplicationBuilderExtensions.cs`**

```csharp
using BaseBackend.Api.Middlewares;
using Serilog;

namespace BaseBackend.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseSharedPipeline(this WebApplication app, IWebHostEnvironment environment)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseSerilogRequestLogging();
        app.UseCors("AllowAll");
        app.MapControllers();
        return app;
    }
}
```

- [ ] **Step 8: Write `Program.cs`**

```csharp
using BaseBackend.Api.Extensions;
using BaseBackend.SharedKernel.Modules;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddModules(builder.Configuration);
builder.Services.AddSharedInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseSharedPipeline(app.Environment);
app.Run();
```

- [ ] **Step 9: Write `appsettings.json`**

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=basebackend;Username=postgres;Password=postgres"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": { "Microsoft.AspNetCore": "Warning" }
    },
    "WriteTo": [ { "Name": "Console" } ]
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 10: Write `appsettings.Development.json`**

```json
{
  "Serilog": {
    "MinimumLevel": { "Default": "Debug" }
  }
}
```

- [ ] **Step 11: Write `Properties/launchSettings.json`**

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "launchUrl": "swagger",
      "applicationUrl": "https://localhost:7080;http://localhost:5080",
      "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
    }
  }
}
```

- [ ] **Step 12: Build the whole solution**

Run: `dotnet build BaseBackend.sln`
Expected: `Build succeeded.` All projects compile.

- [ ] **Step 13: Commit**

```bash
git add -A
git commit -m "feat: add API host with module discovery, validation filter, exception middleware, Serilog and Swagger"
```

---

### Task 7: ProductsController (CRUD endpoints)

**Files:**
- Create: `BaseBackend.Api/Controllers/ProductsController.cs`

**Interfaces:**
- Consumes: `IProductService` (Task 4), `ProductDto`/`CreateProductRequest`/`UpdateProductRequest` (Task 4), `ApiResponse<T>` (Task 2), `BaseApiController` (Task 6).
- Produces: REST endpoints `GET/POST/PUT/DELETE /api/products`.

- [ ] **Step 1: Write `Controllers/ProductsController.cs`**

```csharp
using BaseBackend.Api.Controllers;
using BaseBackend.Catalog.Application.DTOs;
using BaseBackend.Catalog.Application.Services.Interfaces;
using BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc;

namespace BaseBackend.Api.Controllers;

public sealed class ProductsController : BaseApiController
{
    private readonly IProductService _service;

    public ProductsController(IProductService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProductDto>>>> GetAll(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ProductDto>>.Success(
            "Products retrieved", await _service.GetAllAsync(ct)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetById(int id, CancellationToken ct)
    {
        var product = await _service.GetByIdAsync(id, ct);
        return product is null
            ? NotFound(ApiResponse<ProductDto>.Failure("Product not found", "PRODUCT_NOT_FOUND"))
            : Ok(ApiResponse<ProductDto>.Success("Product retrieved", product));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Create(
        CreateProductRequest request, CancellationToken ct)
    {
        var product = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = product.Id },
            ApiResponse<ProductDto>.Success("Product created", product));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Update(
        int id, UpdateProductRequest request, CancellationToken ct)
    {
        var product = await _service.UpdateAsync(id, request, ct);
        return product is null
            ? NotFound(ApiResponse<ProductDto>.Failure("Product not found", "PRODUCT_NOT_FOUND"))
            : Ok(ApiResponse<ProductDto>.Success("Product updated", product));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, ct);
        return deleted
            ? Ok(ApiResponse<object>.Success("Product deleted", null))
            : NotFound(ApiResponse<object>.Failure("Product not found", "PRODUCT_NOT_FOUND"));
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build BaseBackend.Api/BaseBackend.Api.csproj`
Expected: `Build succeeded.`

- [ ] **Step 3: Smoke-check that the app boots and maps the endpoints**

Run: `dotnet run --project BaseBackend.Api --launch-profile https` (then stop with Ctrl+C after it reports listening)
Expected: log shows `Now listening on: https://localhost:7080`. Open `https://localhost:7080/swagger` — the **Products** controller lists GET/POST/PUT/DELETE. (No DB calls are made just by opening Swagger; live calls require Task 8's database.)

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add ProductsController CRUD endpoints"
```

---

### Task 8: Initial EF Core migration

**Files:**
- Create (generated): `src/Modules/Catalog/BaseBackend.Catalog.Infrastructure/Migrations/*`

**Interfaces:**
- Consumes: `CatalogDbContext` + `CatalogDbContextFactory` (Task 5).
- Produces: an `InitialCatalog` migration that creates the `products` table.

- [ ] **Step 1: Ensure the EF Core 10 tool is installed**

Run: `dotnet tool update --global dotnet-ef --version 10.0.*`
(If the machine has no global tool yet, use `dotnet tool install --global dotnet-ef --version 10.0.*`.)
Verify: `dotnet ef --version`
Expected: a `10.0.x` version string.

- [ ] **Step 2: Add the initial migration**

Run from repo root:
```bash
dotnet ef migrations add InitialCatalog \
  --project src/Modules/Catalog/BaseBackend.Catalog.Infrastructure \
  --startup-project BaseBackend.Api \
  --context CatalogDbContext \
  --output-dir Migrations
```
Expected: `Done.` and new files under `.../BaseBackend.Catalog.Infrastructure/Migrations/`.

- [ ] **Step 3: Verify the migration compiles**

Run: `dotnet build src/Modules/Catalog/BaseBackend.Catalog.Infrastructure/BaseBackend.Catalog.Infrastructure.csproj`
Expected: `Build succeeded.`

- [ ] **Step 4 (optional, needs a running PostgreSQL): apply the migration**

Run:
```bash
dotnet ef database update \
  --project src/Modules/Catalog/BaseBackend.Catalog.Infrastructure \
  --startup-project BaseBackend.Api \
  --context CatalogDbContext
```
Expected: `Applying migration 'InitialCatalog'. Done.` A `products` table exists in the `basebackend` database. (Skip if no PostgreSQL is available — the migration files are the deliverable; the README documents this command.)

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add initial Catalog EF Core migration"
```

---

### Task 9: Architecture & convention tests

**Files:**
- Create: `tests/BaseBackend.ArchitectureTests/BaseBackend.ArchitectureTests.csproj`, `BoundaryTests.cs`, `ModuleConventionTests.cs`

**Interfaces:**
- Consumes: assemblies of `Product` (Domain), `CatalogDbContext` (Infrastructure), `BaseApiController`/`ProductsController` (Api), `AddModules` (SharedKernel), `IProductService`/`ICatalogModule`.
- Produces: enforcement of the Global Constraints dependency rules.

- [ ] **Step 1: Create the test project and references**

```bash
dotnet new xunit -n BaseBackend.ArchitectureTests -o tests/BaseBackend.ArchitectureTests
rm tests/BaseBackend.ArchitectureTests/UnitTest1.cs
dotnet sln add tests/BaseBackend.ArchitectureTests/BaseBackend.ArchitectureTests.csproj --solution-folder tests
```

Set `BaseBackend.ArchitectureTests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="NetArchTest.Rules" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\BaseBackend.Api\BaseBackend.Api.csproj" />
    <ProjectReference Include="..\..\src\Modules\Catalog\BaseBackend.Catalog.Domain\BaseBackend.Catalog.Domain.csproj" />
    <ProjectReference Include="..\..\src\Modules\Catalog\BaseBackend.Catalog.Infrastructure\BaseBackend.Catalog.Infrastructure.csproj" />
    <ProjectReference Include="..\..\src\SharedKernel\BaseBackend.SharedKernel\BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write `BoundaryTests.cs`**

```csharp
using System.Reflection;
using BaseBackend.Api.Controllers;
using BaseBackend.Catalog.Domain.Entities;
using BaseBackend.Catalog.Infrastructure.Data;
using NetArchTest.Rules;
using Xunit;

namespace BaseBackend.ArchitectureTests;

public class BoundaryTests
{
    private static readonly Assembly Domain = typeof(Product).Assembly;
    private static readonly Assembly Infrastructure = typeof(CatalogDbContext).Assembly;
    private static readonly Assembly Api = typeof(BaseApiController).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_Infrastructure()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOn("BaseBackend.Catalog.Infrastructure")
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Domain_should_not_depend_on_EntityFrameworkCore()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_Api()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot().HaveDependencyOn("BaseBackend.Api")
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Controllers_should_inherit_BaseApiController()
    {
        var result = Types.InAssembly(Api)
            .That().HaveNameEndingWith("Controller")
            .And().AreClasses()
            .And().AreNotAbstract()
            .Should().Inherit(typeof(BaseApiController))
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result)
        => result.IsSuccessful ? "" : string.Join(", ", result.FailingTypeNames ?? new List<string>());
}
```

- [ ] **Step 3: Write `ModuleConventionTests.cs`** (proves `AddModules` auto-discovers and registers the Catalog module)

```csharp
using BaseBackend.Catalog.Application.Services.Interfaces;
using BaseBackend.Catalog.Contracts;
using BaseBackend.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaseBackend.ArchitectureTests;

public class ModuleConventionTests
{
    [Fact]
    public void AddModules_registers_catalog_services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=u;Password=p"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddModules(configuration);

        Assert.Contains(services, d => d.ServiceType == typeof(IProductService));
        Assert.Contains(services, d => d.ServiceType == typeof(ICatalogModule));
    }
}
```

- [ ] **Step 4: Run the architecture tests — verify they pass**

Run: `dotnet test tests/BaseBackend.ArchitectureTests/BaseBackend.ArchitectureTests.csproj`
Expected: PASS, 5 tests passed.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test BaseBackend.sln`
Expected: PASS — 14 (Catalog.UnitTests) + 5 (ArchitectureTests) = 19 tests passed.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "test: add architecture boundary and module-convention tests"
```

---

### Task 10: Docs — shared template + ARCHITECTURE_OVERVIEW, SOLUTION_STRUCTURE, PROJECT_LIBRARIES

**Files:**
- Create: `docs/ARCHITECTURE_OVERVIEW.html`, `docs/SOLUTION_STRUCTURE.html`, `docs/PROJECT_LIBRARIES.html`

**Shared design system (use verbatim in every docs page's `<head><style>`):** the BaseProject CSS, with backend layer colors added. Each page reuses this exact block, changing only the page `<title>`, the `header.page` title/subtitle, and the body content.

```html
<style>
  :root {
    --bg: #f6f8fa; --card: #ffffff; --border: #e1e4e8; --text: #1f2328; --muted: #57606a;
    --accent: #2563eb; --accent-soft: #eaf1ff; --code-bg: #0f172a; --code-text: #e2e8f0;
    /* backend layer colors */
    --dom: #166534;   --dom-bg: #dcfce7;   /* Domain */
    --app: #5b21b6;   --app-bg: #ede9fe;   /* Application */
    --inf: #9a3412;   --inf-bg: #ffedd5;   /* Infrastructure */
    --con: #0f766e;   --con-bg: #ccfbf1;   /* Contracts */
    --host: #2563eb;  --host-bg: #eaf1ff;  /* Host/Api */
    --shared: #475569; --shared-bg: #e2e8f0; /* SharedKernel */
  }
  * { box-sizing: border-box; }
  body { margin: 0; padding: 0 16px 72px; background: var(--bg); color: var(--text);
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; }
  .wrap { max-width: 980px; margin: 0 auto; }
  header.page { padding: 40px 0 22px; border-bottom: 2px solid var(--border); margin-bottom: 8px; }
  header.page h1 { margin: 0 0 6px; font-size: 1.95rem; }
  header.page h1 .mono { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; color: var(--accent); }
  header.page p { margin: 0; color: var(--muted); font-size: 0.98rem; }
  .banner { background: var(--accent-soft); border: 1px solid #c7dbff; border-radius: 12px; padding: 14px 18px; margin: 22px 0 6px; font-size: 0.95rem; }
  .banner strong { color: var(--accent); }
  .legend { display: flex; flex-wrap: wrap; gap: 10px; margin: 18px 0 6px; font-size: 0.8rem; align-items: center; }
  .legend .lab { color: var(--muted); font-weight: 600; }
  .tag { display: inline-block; padding: 2px 9px; border-radius: 999px; font-weight: 600; font-size: 0.74rem; letter-spacing: .2px; }
  .tag.dom { color: var(--dom); background: var(--dom-bg); }
  .tag.app { color: var(--app); background: var(--app-bg); }
  .tag.inf { color: var(--inf); background: var(--inf-bg); }
  .tag.con { color: var(--con); background: var(--con-bg); }
  .tag.host { color: var(--host); background: var(--host-bg); }
  .tag.shared { color: var(--shared); background: var(--shared-bg); }
  h2.section { font-size: 1.22rem; margin: 42px 0 14px; padding-bottom: 6px; border-bottom: 1px solid var(--border); }
  p.lead { color: var(--muted); margin: 0 0 18px; }
  .flow { display: flex; flex-direction: column; align-items: stretch; gap: 0; margin: 8px 0 6px; }
  .node { border: 1px solid var(--border); border-left-width: 5px; background: var(--card); border-radius: 10px; padding: 12px 16px; }
  .node .nh { font-weight: 700; font-size: 0.98rem; }
  .node .nf { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 0.78rem; color: var(--muted); }
  .node.dom { border-left-color: var(--dom); } .node.app { border-left-color: var(--app); }
  .node.inf { border-left-color: var(--inf); } .node.con { border-left-color: var(--con); }
  .node.host { border-left-color: var(--host); } .node.shared { border-left-color: var(--shared); }
  .arrow { align-self: center; color: var(--muted); font-size: 0.78rem; padding: 6px 0; text-align: center; }
  .arrow .down { font-size: 1.1rem; line-height: 1; }
  .arrow .what { display: inline-block; background: #fff; border: 1px solid var(--border); border-radius: 999px; padding: 1px 10px; font-size: 0.72rem; margin-left: 6px; }
  .card { background: var(--card); border: 1px solid var(--border); border-radius: 12px; padding: 16px 18px; margin: 14px 0; }
  .card.dom { border-left: 5px solid var(--dom); } .card.app { border-left: 5px solid var(--app); }
  .card.inf { border-left: 5px solid var(--inf); } .card.con { border-left: 5px solid var(--con); }
  .card.host { border-left: 5px solid var(--host); } .card.shared { border-left: 5px solid var(--shared); }
  .card .ch { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; margin-bottom: 4px; }
  .card .ch h3 { margin: 0; font-size: 1.02rem; }
  .card .file { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 0.76rem; color: var(--muted); }
  .card p { margin: 8px 0; font-size: 0.93rem; }
  pre { background: var(--code-bg); color: var(--code-text); border-radius: 10px; padding: 14px 16px; overflow-x: auto; font-size: 0.82rem; line-height: 1.5; margin: 10px 0 2px; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
  code.inline { background: #eef1f4; color: #0f172a; border-radius: 5px; padding: 1px 6px; font-size: 0.86em; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
  .tok-c { color: #94a3b8; } .tok-k { color: #93c5fd; } .tok-s { color: #86efac; } .tok-t { color: #fca5a5; }
  ol.steps { counter-reset: step; list-style: none; padding-left: 0; }
  ol.steps > li { position: relative; padding: 12px 14px 12px 52px; margin: 10px 0; background: var(--card); border: 1px solid var(--border); border-radius: 10px; }
  ol.steps > li::before { counter-increment: step; content: counter(step); position: absolute; left: 14px; top: 12px; width: 26px; height: 26px; border-radius: 50%; background: var(--accent); color: #fff; font-weight: 700; display: flex; align-items: center; justify-content: center; font-size: 0.85rem; }
  ol.steps > li b { display: block; margin-bottom: 2px; }
  table.conv { width: 100%; border-collapse: collapse; font-size: 0.88rem; margin: 6px 0; }
  table.conv th, table.conv td { border: 1px solid var(--border); padding: 8px 10px; text-align: left; vertical-align: top; }
  table.conv th { background: #f0f3f6; }
  footer.page { margin-top: 48px; padding-top: 18px; border-top: 1px solid var(--border); color: var(--muted); font-size: 0.86rem; }
</style>
```

**Tag/legend convention (every page):** Domain `tag.dom`, Application `tag.app`, Infrastructure `tag.inf`, Contracts `tag.con`, Host/Api `tag.host`, SharedKernel `tag.shared`.

- [ ] **Step 1: Write `docs/ARCHITECTURE_OVERVIEW.html`** (the landing page; this is the exemplar — pages 2–5 reuse its skeleton)

Use this full structure, embedding the shared `<style>` from above:
```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Architecture Overview — BaseBackend</title>
  <!-- shared <style> block here -->
</head>
<body>
  <div class="wrap">
    <header class="page">
      <h1><span class="mono">BaseBackend</span> — Architecture Overview</h1>
      <p>A .NET 10 modular monolith. Read this first, then Solution Structure, Request Flow, and Module Anatomy.</p>
    </header>

    <div class="banner"><strong>Modular monolith:</strong> one deployable host, many self-contained modules.
      Modules never reference each other's internals — only their <code class="inline">.Contracts</code>.</div>

    <div class="legend"><span class="lab">Layers:</span>
      <span class="tag dom">Domain</span><span class="tag app">Application</span>
      <span class="tag inf">Infrastructure</span><span class="tag con">Contracts</span>
      <span class="tag host">Host / Api</span><span class="tag shared">SharedKernel</span>
    </div>

    <h2 class="section">The big picture</h2>
    <p class="lead">The host discovers modules at startup and wires them in. Each module owns its data and exposes a thin contract.</p>
    <!-- Insert the SVG diagram described below -->

    <h2 class="section">A module's four layers</h2>
    <!-- one .card per layer: Domain (dom), Application (app), Infrastructure (inf), Contracts (con).
         Each card: <div class="ch"><span class="tag X">Layer</span><h3>Name</h3></div> + one-paragraph responsibility + a "Depends on" line. -->

    <h2 class="section">Dependency rules</h2>
    <table class="conv">
      <tr><th>Layer</th><th>May reference</th></tr>
      <tr><td>Domain</td><td>SharedKernel</td></tr>
      <tr><td>Application</td><td>Domain, SharedKernel</td></tr>
      <tr><td>Infrastructure</td><td>Domain, Application, Contracts, SharedKernel</td></tr>
      <tr><td>Another module</td><td>only this module's <code>.Contracts</code></td></tr>
    </table>
    <p>These rules are enforced by <code class="inline">tests/BaseBackend.ArchitectureTests</code>.</p>

    <footer class="page">BaseBackend docs · see SOLUTION_STRUCTURE.html, REQUEST_FLOW.html, MODULE_ANATOMY.html, PROJECT_LIBRARIES.html</footer>
  </div>
</body>
</html>
```

Insert this **SVG block diagram** under "The big picture" (host on top, three layers below, SharedKernel beside):
```html
<svg viewBox="0 0 760 300" width="100%" role="img" aria-label="BaseBackend architecture">
  <style>
    .b{rx:10;ry:10;stroke:#e1e4e8;stroke-width:1.5;}
    .t{font:600 13px -apple-system,Segoe UI,Roboto,sans-serif;fill:#1f2328;}
    .s{font:11px ui-monospace,Consolas,monospace;fill:#57606a;}
    .ln{stroke:#94a3b8;stroke-width:1.5;marker-end:url(#a);}
  </style>
  <defs><marker id="a" markerWidth="9" markerHeight="9" refX="7" refY="3" orient="auto">
    <path d="M0,0 L7,3 L0,6 Z" fill="#94a3b8"/></marker></defs>
  <rect class="b" x="250" y="20" width="260" height="46" fill="#eaf1ff"/>
  <text class="t" x="270" y="40">BaseBackend.Api (host)</text>
  <text class="s" x="270" y="56">Program.cs · AddModules() · pipeline</text>
  <rect class="b" x="120" y="120" width="170" height="120" fill="#dcfce7"/>
  <text class="t" x="138" y="142">Catalog.Domain</text>
  <rect class="b" x="300" y="120" width="170" height="120" fill="#ede9fe"/>
  <text class="t" x="318" y="142">Catalog.Application</text>
  <rect class="b" x="480" y="120" width="170" height="120" fill="#ffedd5"/>
  <text class="t" x="498" y="142">Catalog.Infrastructure</text>
  <rect class="b" x="300" y="255" width="170" height="34" fill="#ccfbf1"/>
  <text class="t" x="318" y="277">Catalog.Contracts</text>
  <line class="ln" x1="380" y1="66" x2="380" y2="118"/>
</svg>
```

- [ ] **Step 2: Write `docs/SOLUTION_STRUCTURE.html`**

Reuse the shared skeleton. Title `Solution Structure — BaseBackend`. Content:
- A banner: "Every file, what it's for, and whether you touch it."
- A legend of three status tags (reuse `.tag` styles, label them): **You edit** (`tag.dom`), **Auto-generated** (`tag.inf`), **Set-once config** (`tag.shared`).
- A `<pre>` ASCII tree copied from this plan's **File Structure** section.
- One `.card` per top-level item with a status tag + `.file` path + one-paragraph purpose, for: `BaseBackend.sln`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `BaseBackend.Api/`, `src/SharedKernel/`, `src/Modules/Catalog/`, `tests/`, `docs/`.
- A "Mental model" section: 3–4 sentences — host at root, shared abstractions in SharedKernel, one folder per module, tests mirror the modules.

- [ ] **Step 3: Write `docs/PROJECT_LIBRARIES.html`**

Reuse the shared skeleton. Title `Project Libraries — BaseBackend`. Content:
- Banner explaining central package management: "All versions live in `Directory.Packages.props`; projects reference packages without versions."
- A `<pre>` showing the real `Directory.Packages.props` `<ItemGroup>` (copy from Task 1) and a sample versionless `<PackageReference Include="FluentValidation" />`.
- A **runtime** `table.conv` with columns Package / Version / Role / Used for, one row per: Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL, FluentValidation, FluentValidation.DependencyInjectionExtensions, Riok.Mapperly, Serilog.AspNetCore, Swashbuckle.AspNetCore. Use the exact versions from Task 1.
- A **test** `table.conv`: Microsoft.NET.Test.Sdk, xunit, xunit.runner.visualstudio, Moq, NetArchTest.Rules.

- [ ] **Step 4: Verify the three pages render**

Run: `start docs/ARCHITECTURE_OVERVIEW.html` (Windows) — confirm it opens, the header, banner, legend tags, layer cards, the SVG diagram, and the dependency table all display with the BaseProject styling. Repeat for the other two.
Also confirm each file is self-contained (no external `<link>`/`<script>` references).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs: add architecture overview, solution structure, and libraries pages"
```

---

### Task 11: Docs — REQUEST_FLOW + MODULE_ANATOMY

**Files:**
- Create: `docs/REQUEST_FLOW.html`, `docs/MODULE_ANATOMY.html`

**Interfaces:**
- Consumes: the shared `<style>` block and tag/legend convention from Task 10; real code from Tasks 5–7.

- [ ] **Step 1: Write `docs/REQUEST_FLOW.html`**

Reuse the shared skeleton. Title `Request Flow — BaseBackend`. Worked example: **`GET /api/products`**. Sections:
1. Banner: "Follow one request from HTTP to PostgreSQL and back as `ApiResponse<T>`."
2. Legend (the six layer tags).
3. **Flow diagram** using `.flow` / `.node` / `.arrow`: nodes in order — `ProductsController` (host), `ValidationFilter` (host), `IProductService → ProductService` (app/inf), `IProductRepository → ProductRepository` (inf), `CatalogDbContext` (inf), `PostgreSQL`. Each `.node` has `.nh` title + `.nf` file path; `.arrow` between them labels the call (`.what`).
4. **SVG sequence diagram** (participants across the top, lifelines, numbered call arrows down, dashed return arrows up). Use this skeleton and fill the 5 calls / 5 returns:
```html
<svg viewBox="0 0 720 320" width="100%" role="img" aria-label="GET /api/products sequence">
  <style>.p{fill:#eaf1ff;stroke:#c7dbff;rx:6;ry:6;} .pt{font:600 12px -apple-system,sans-serif;fill:#1f2328;}
    .ll{stroke:#cbd5e1;stroke-dasharray:3 3;} .call{stroke:#2563eb;stroke-width:1.5;marker-end:url(#mc);}
    .ret{stroke:#16a34a;stroke-width:1.3;stroke-dasharray:5 3;marker-end:url(#mr);} .ms{font:11px -apple-system,sans-serif;fill:#334155;}</style>
  <defs>
    <marker id="mc" markerWidth="9" markerHeight="9" refX="7" refY="3" orient="auto"><path d="M0,0 L7,3 L0,6 Z" fill="#2563eb"/></marker>
    <marker id="mr" markerWidth="9" markerHeight="9" refX="7" refY="3" orient="auto"><path d="M0,0 L7,3 L0,6 Z" fill="#16a34a"/></marker>
  </defs>
  <!-- participants: Controller(80), Service(240), Repository(400), DbContext(540), PostgreSQL(660) -->
  <!-- draw <rect class="p"> + <text class="pt"> for each, vertical <line class="ll"> lifelines,
       then <line class="call"> / <line class="ret"> with <text class="ms"> labels for each numbered step -->
</svg>
```
5. **Layer cards** (`.card` with matching layer class), one per stop, each with a real code snippet copied from the implementation:
   - `ProductsController.GetAll` (host) — from Task 7.
   - `ProductService.GetAllAsync` (inf) — from Task 5.
   - `ProductRepository.GetAllAsync` (inf) — from Task 5.
   - `CatalogDbContext` (inf) — from Task 5.
6. **Conventions** `table.conv`: response envelope (`ApiResponse<T>`), validation (`ValidationFilter` + FluentValidation), errors (`ExceptionHandlingMiddleware`), routing (`api/[controller]` via `BaseApiController`).
7. **"Add a new endpoint"** `ol.steps`: (1) add a DTO + validator in Application, (2) add a method to `IProductService` + implement in `ProductService`, (3) add a repository method if needed, (4) add the controller action returning `ApiResponse<T>`, (5) run `dotnet test`.

- [ ] **Step 2: Write `docs/MODULE_ANATOMY.html`**

Reuse the shared skeleton. Title `Module Anatomy — BaseBackend`. Sections:
1. Banner: "Every module is four projects. Copy `Catalog` to start a new one."
2. A `<pre>` tree of `src/Modules/Catalog/` (the four projects + their key files, from the File Structure section).
3. Four `.card`s (dom/app/inf/con), each: the project name, its `.file` path, what it holds, and what it may depend on (from the dependency table).
4. A `.card shared` for **SharedKernel** building blocks: `ApiResponse<T>`, `BaseEntity`, `DomainException`, `IModule` / `AddModules` — each with a one-line description.
5. **The `IModule` convention**: a `<pre>` with the real `CatalogModule.Register` body (from Task 5) and a sentence that the host auto-discovers any `IModule` in `BaseBackend.*.dll`.
6. **"Add a new module"** `ol.steps`: (1) create the four projects under `src/Modules/<Name>/` with the right references, (2) put entities in Domain, DTOs/validators/mapping/service-interface in Application, the cross-module interface in Contracts, (3) implement DbContext + repository + service + `class <Name>Module : IModule` in Infrastructure, (4) reference the new Infrastructure project from `BaseBackend.Api`, (5) add an EF migration, (6) add unit + architecture coverage, (7) `dotnet test`.

- [ ] **Step 3: Verify both pages render**

Run: `start docs/REQUEST_FLOW.html` and `start docs/MODULE_ANATOMY.html` — confirm the flow diagram, SVG sequence diagram, layer cards with real code, conventions table, and numbered steps all display correctly and self-contained.

- [ ] **Step 4: Final full verification**

Run: `dotnet build BaseBackend.sln && dotnet test BaseBackend.sln`
Expected: build succeeds; 18 tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs: add request-flow and module-anatomy pages"
```

---

## Self-Review

**Spec coverage** (each spec section → task):
- Solution structure & tooling → Task 1. ✓
- SharedKernel (ApiResponse, BaseEntity, DomainException, IModule, AddModules) → Task 2. ✓
- Sample `Catalog` module: Domain → Task 3; Application + Contracts → Task 4; Infrastructure → Task 5. ✓
- Host wiring (Program, extensions, filter, middleware, BaseApiController) → Task 6; ProductsController → Task 7. ✓
- Database & per-module migrations → Task 8. ✓
- Testing: Catalog.UnitTests → Tasks 3–5; ArchitectureTests → Task 9. ✓
- Five HTML docs → Tasks 10–11. ✓
- Central package management, `Directory.Build.props`, `IModule` convention, `global.json`, `.editorconfig`, `.gitignore`, `README` → Task 1 + Task 2. ✓
- Out-of-scope items (auth, extra modules, integration tests, CI/CD) → correctly absent. ✓

**Placeholder scan:** no TBD/TODO; every code step shows complete code; doc tasks give the shared style block verbatim, the exemplar page in full, real SVG skeletons, and explicit per-section content with the exact source snippets to copy. ✓

**Type consistency:** `ApiResponse<T>.Success/Failure`, `IProductService` (GetAll/GetById/Create/Update/Delete), `IProductRepository` (GetAll/GetById/Add/Remove/Exists/SaveChanges), `ICatalogModule.ProductExistsAsync`, `IModule.Register`, `AddModules`, `AddSharedInfrastructure`, `UseSharedPipeline`, `BaseApiController` — names are identical across producing and consuming tasks. ✓
```
