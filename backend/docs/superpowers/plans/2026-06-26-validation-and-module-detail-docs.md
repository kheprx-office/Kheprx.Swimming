# Validation Reference + Module Folder-Detail Docs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make all validation failures return one consistent `ApiResponse<T>` envelope, and deepen the BaseBackend docs with a new `VALIDATION.html` reference and a folder-by-folder section in `MODULE_ANATOMY.html`.

**Architecture:** One small host-side code change (an `InvalidModelStateResponseFactory` backed by a pure, unit-tested `ModelStateResponse.From` helper) plus three documentation changes. The base is already built and green (19 tests). All work happens on `master` in `C:\Users\envnt\Desktop\Base Backend`.

**Tech Stack:** .NET 10, ASP.NET Core, xUnit; offline HTML docs (inline CSS design system, inline SVG).

## Global Constraints

- **Target framework `net10.0`** is set once in `Directory.Build.props`; no child `.csproj` declares `<TargetFramework>`.
- **Central package management:** every `<PackageReference>` is versionless; versions live only in `Directory.Packages.props` (already contains `Microsoft.NET.Test.Sdk` 17.12.0, `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2).
- **`TreatWarningsAsErrors=true`** + NuGet audit on: the solution must build warning-clean and audit-clean with **no suppressions** (`NoWarn`/`NuGetAuditSuppress`/`WarningsNotAsErrors` for NU190x are forbidden).
- **Docs are fully offline/self-contained:** no external `<link>`/`<script src>`/CDN/web-font; the only permitted `http` occurrence is `xmlns="http://www.w3.org/2000/svg"` on inline `<svg>`. Each page reuses the shared `<style>` block verbatim and the layer tag classes (`dom`/`app`/`inf`/`con`/`host`/`shared`).
- **Never write "AutoMapper"** (mapping is Mapperly / `Riok.Mapperly`). **Never reference a generic `BaseEntity<TId>`** — `BaseEntity` is non-generic with an `int Id`.
- **Response envelope:** every endpoint and every validation failure returns `ApiResponse<T>` (`successStatus`, `message`, `error`, `data`). The validation message is exactly `"Validation failed"`.
- Files end with a trailing newline. Run all commands from the repo root unless stated otherwise. Commit after each task with the message in its final step.

---

## File Structure

```
BaseBackend.Api/
├── Extensions/ServiceCollectionExtensions.cs   (modify: chain ConfigureApiBehaviorOptions)
└── Validation/ModelStateResponse.cs            (new: pure helper)
tests/
└── BaseBackend.Api.UnitTests/                  (new project)
    ├── BaseBackend.Api.UnitTests.csproj
    └── ModelStateResponseTests.cs
docs/
├── VALIDATION.html                             (new page)
├── MODULE_ANATOMY.html                         (modify: add folder-by-folder section + footer)
├── REQUEST_FLOW.html                           (modify: footer + validation row link)
├── ARCHITECTURE_OVERVIEW.html                  (modify: footer)
├── SOLUTION_STRUCTURE.html                     (modify: footer)
└── PROJECT_LIBRARIES.html                      (modify: footer)
```

---

### Task 1: Unified validation envelope (code fix + Api.UnitTests)

**Files:**
- Create: `tests/BaseBackend.Api.UnitTests/BaseBackend.Api.UnitTests.csproj`, `tests/BaseBackend.Api.UnitTests/ModelStateResponseTests.cs`
- Create: `BaseBackend.Api/Validation/ModelStateResponse.cs`
- Modify: `BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs`

**Interfaces:**
- Consumes: `ApiResponse<object>.Failure(string message, string error)` (SharedKernel), `ValidationFilter` (already registered).
- Produces: `public static ApiResponse<object> BaseBackend.Api.Validation.ModelStateResponse.From(ModelStateDictionary modelState)`.

- [ ] **Step 1: Create the `BaseBackend.Api.UnitTests` project and add it to the solution**

```bash
dotnet new xunit -n BaseBackend.Api.UnitTests -o tests/BaseBackend.Api.UnitTests
rm tests/BaseBackend.Api.UnitTests/UnitTest1.cs
dotnet sln add tests/BaseBackend.Api.UnitTests/BaseBackend.Api.UnitTests.csproj --solution-folder tests
dotnet add tests/BaseBackend.Api.UnitTests/BaseBackend.Api.UnitTests.csproj reference BaseBackend.Api/BaseBackend.Api.csproj
```

Then replace `tests/BaseBackend.Api.UnitTests/BaseBackend.Api.UnitTests.csproj` with versionless content (central package management; `dotnet new xunit` writes versions and may add `coverlet` — remove them):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\BaseBackend.Api\BaseBackend.Api.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write the failing tests `ModelStateResponseTests.cs`**

```csharp
using BaseBackend.Api.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace BaseBackend.Api.UnitTests;

public class ModelStateResponseTests
{
    [Fact]
    public void From_invalid_model_state_returns_failure_envelope()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("name", "The name field is required.");

        var response = ModelStateResponse.From(modelState);

        Assert.False(response.SuccessStatus);
        Assert.Equal("Validation failed", response.Message);
        Assert.Contains("The name field is required.", response.Error);
    }

    [Fact]
    public void From_joins_multiple_error_messages()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("name", "Name required.");
        modelState.AddModelError("price", "Price invalid.");

        var response = ModelStateResponse.From(modelState);

        Assert.Contains("Name required.", response.Error);
        Assert.Contains("Price invalid.", response.Error);
    }
}
```

- [ ] **Step 3: Run the tests — verify they FAIL (does not compile yet)**

Run: `dotnet test tests/BaseBackend.Api.UnitTests/BaseBackend.Api.UnitTests.csproj`
Expected: FAIL — compile error, `ModelStateResponse` does not exist in `BaseBackend.Api.Validation`.

- [ ] **Step 4: Create the helper `BaseBackend.Api/Validation/ModelStateResponse.cs`**

```csharp
using BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BaseBackend.Api.Validation;

public static class ModelStateResponse
{
    public static ApiResponse<object> From(ModelStateDictionary modelState)
    {
        var errors = string.Join("; ", modelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message)));

        return ApiResponse<object>.Failure("Validation failed", errors);
    }
}
```

- [ ] **Step 5: Run the tests — verify they PASS**

Run: `dotnet test tests/BaseBackend.Api.UnitTests/BaseBackend.Api.UnitTests.csproj`
Expected: PASS, 2 tests passed, output pristine.

- [ ] **Step 6: Wire the factory in `ServiceCollectionExtensions.cs`**

Add two usings at the top of `BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs` (after the existing `using` lines):
```csharp
using BaseBackend.Api.Validation;
using Microsoft.AspNetCore.Mvc;
```

Replace the existing `AddControllers(...).AddJsonOptions(...)` chain with the version that also configures the API behavior options:
```csharp
        services.AddControllers(options => options.Filters.Add<ValidationFilter>())
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
            .ConfigureApiBehaviorOptions(options =>
                options.InvalidModelStateResponseFactory = context =>
                    new BadRequestObjectResult(ModelStateResponse.From(context.ModelState)));
```

- [ ] **Step 7: Build the whole solution and run the full test suite**

Run: `dotnet build BaseBackend.sln`
Expected: `Build succeeded.` 0 warnings, 0 errors.

Run: `dotnet test BaseBackend.sln`
Expected: PASS — 14 (Catalog.UnitTests) + 5 (ArchitectureTests) + 2 (Api.UnitTests) = **21 tests passed**.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: unify validation envelope via InvalidModelStateResponseFactory + Api.UnitTests"
```

---

### Task 2: New `docs/VALIDATION.html` page

**Files:**
- Create: `docs/VALIDATION.html`

**Interfaces:**
- Consumes: the shared `<style>` block and layer tag classes from the existing pages.

- [ ] **Step 1: Create `docs/VALIDATION.html` with the shared head + style**

Start the file with the standard skeleton. **Copy the entire `<style>…</style>` block verbatim from `docs/ARCHITECTURE_OVERVIEW.html`** (the shared design system — `:root` variables, `.tag`, `.card`, `.node`, `.arrow`, `.flow`, `table.conv`, `ol.steps`, `footer.page`, etc.). Use this head/title and header:
```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Validation — BaseBackend</title>
  <style>
  /* <-- paste the shared style block from ARCHITECTURE_OVERVIEW.html here, unchanged --> */
  </style>
</head>
<body>
  <div class="wrap">
    <header class="page">
      <h1><span class="mono">BaseBackend</span> — Validation</h1>
      <p>Five layers of validation, from the HTTP edge to the database. Each owns a different kind of rule.</p>
    </header>

    <div class="banner"><strong>Fail fast, cheapest first.</strong> A request is checked at five points on its way in. Body validation (layers 2–4) all return the same <code class="inline">ApiResponse&lt;T&gt;</code> envelope.</div>

    <div class="legend"><span class="lab">Runs in:</span>
      <span class="tag host">Host / Api</span><span class="tag app">Application</span>
      <span class="tag dom">Domain</span><span class="tag inf">Infrastructure</span>
    </div>
```

- [ ] **Step 2: Add the validation-flow diagram (five nodes, pipeline order)**

After the legend, add a `.flow` block with one `.node` per layer (colored by where it runs) and an `.arrow` between them. Use this content:
```html
    <h2 class="section">The validation pipeline</h2>
    <p class="lead">A request passes these checks in order. The first one that fails short-circuits with a response.</p>
    <div class="flow">
      <div class="node host"><div class="nh">1 · Route constraints</div><div class="nf">[HttpGet("{id:int}")] — malformed id never matches → 404</div></div>
      <div class="arrow"><span class="down">&#8595;</span><span class="what">route matched</span></div>
      <div class="node host"><div class="nh">2 · Model binding / [ApiController]</div><div class="nf">missing or malformed fields → 400 ApiResponse "Validation failed"</div></div>
      <div class="arrow"><span class="down">&#8595;</span><span class="what">body bound</span></div>
      <div class="node app"><div class="nh">3 · FluentValidation (ValidationFilter)</div><div class="nf">request-DTO business rules → 400 ApiResponse "Validation failed"</div></div>
      <div class="arrow"><span class="down">&#8595;</span><span class="what">request valid</span></div>
      <div class="node dom"><div class="nh">4 · Domain invariants</div><div class="nf">entity guards throw DomainException → 400 ApiResponse "DOMAIN_RULE_VIOLATION"</div></div>
      <div class="arrow"><span class="down">&#8595;</span><span class="what">invariants hold</span></div>
      <div class="node inf"><div class="nh">5 · Persistence constraints</div><div class="nf">EF/DB integrity backstop → 500 on breach</div></div>
    </div>
```

- [ ] **Step 3: Add the five layer cards with real code snippets**

Add a `<h2 class="section">The five layers</h2>` then one `.card` per layer (colored by the layer it runs in). Use the real code below. Apply the same `tok-k`/`tok-s`/`tok-c`/`tok-t` highlight spans the other pages use (keywords, strings, comments, types) — match the style of `MODULE_ANATOMY.html` code blocks.

Card 1 — `host` — **Route constraints** · file `BaseBackend.Api/Controllers/ProductsController.cs`:
```csharp
[HttpGet("{id:int}")]
public async Task<ActionResult<ApiResponse<ProductDto>>> GetById(int id, CancellationToken ct)
```
Text: the `:int` route constraint rejects a non-integer id at routing time — the route simply does not match, so the client gets a 404 and the action never runs. Owns: route value shape/type.

Card 2 — `host` — **Model binding / `[ApiController]`** · files `BaseBackend.Api/Validation/ModelStateResponse.cs` + `BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs`:
```csharp
.ConfigureApiBehaviorOptions(o =>
    o.InvalidModelStateResponseFactory = ctx =>
        new BadRequestObjectResult(ModelStateResponse.From(ctx.ModelState)));

// ModelStateResponse.From(...)
public static ApiResponse<object> From(ModelStateDictionary modelState) =>
    ApiResponse<object>.Failure("Validation failed",
        string.Join("; ", modelState.Values
            .SelectMany(e => e.Errors).Select(e => e.ErrorMessage)));
```
Text: `[ApiController]` (on `BaseApiController`) runs automatic model-state validation. Non-nullable request properties are treated as required, so a missing or wrong-typed field fails binding. The `InvalidModelStateResponseFactory` converts that failure into the same `ApiResponse.Failure("Validation failed", …)` envelope the rest of the stack uses. Owns: presence/shape/type of the request body.

Card 3 — `app` — **FluentValidation** · files `.Application/Validators/ProductRequestValidators.cs` (run by `BaseBackend.Api/Filters/ValidationFilter.cs`):
```csharp
public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
```
Text: the global `ValidationFilter` resolves the matching `IValidator<T>` for each action argument from DI and runs it before the action; on failure it returns `400 ApiResponse.Failure("Validation failed", joinedErrors)`. Validators are auto-registered by `AddValidatorsFromAssemblies`. Owns: business rules on the request DTO (required, length, range).

Card 4 — `dom` — **Domain invariants** · files `.Domain/Entities/Product.cs` → `BaseBackend.Api/Middlewares/ExceptionHandlingMiddleware.cs`:
```csharp
public void ChangePrice(decimal price)
{
    if (price <= 0)
        throw new InvalidProductException("Product price must be greater than zero.");
    Price = price;
    Touch();
}
```
Text: invariants that must hold no matter how the entity is created or mutated live as guards inside entity methods, throwing an `InvalidProductException : DomainException`. `ExceptionHandlingMiddleware` catches any `DomainException` and returns `400 ApiResponse.Failure(message, "DOMAIN_RULE_VIOLATION")`. This is the last line of defense — it holds even if a caller bypasses the DTO validators. Owns: entity invariants.

Card 5 — `inf` — **Persistence constraints** · file `.Infrastructure/Configurations/ProductConfiguration.cs`:
```csharp
builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
builder.Property(p => p.Description).HasMaxLength(2000);
builder.Property(p => p.Price).HasColumnType("numeric(18,2)");
```
Text: the EF Core fluent configuration mirrors the DTO rules as database constraints — defense in depth. A violation that somehow reaches the database throws `DbUpdateException`, which `ExceptionHandlingMiddleware` turns into a generic `500`. You should rarely hit this layer; it exists so the data can never be corrupted. Owns: storage integrity.

- [ ] **Step 4: Add the "Which layer owns this rule?" decision table**

```html
    <h2 class="section">Which layer owns this rule?</h2>
    <table class="conv">
      <tr><th>Rule kind</th><th>Lives in</th><th>Mechanism</th></tr>
      <tr><td>Required / length / format / range on a request field</td><td><span class="tag app">Application</span></td><td><code class="inline">AbstractValidator&lt;TRequest&gt;</code> (FluentValidation)</td></tr>
      <tr><td>Type/shape of a route value</td><td><span class="tag host">Host / Api</span></td><td>route constraint <code class="inline">{id:int}</code></td></tr>
      <tr><td>Missing / malformed request body</td><td><span class="tag host">Host / Api</span></td><td>model binding + <code class="inline">InvalidModelStateResponseFactory</code></td></tr>
      <tr><td>Invariant that must always hold for the entity</td><td><span class="tag dom">Domain</span></td><td>guard in the entity method throwing a <code class="inline">DomainException</code></td></tr>
      <tr><td>Uniqueness / referential integrity</td><td><span class="tag inf">Infrastructure</span></td><td>repository check and/or DB constraint</td></tr>
      <tr><td>Last-line storage integrity</td><td><span class="tag inf">Infrastructure</span></td><td>EF fluent config (<code class="inline">HasMaxLength</code>, <code class="inline">IsRequired</code>)</td></tr>
    </table>
```

- [ ] **Step 5: Add the unified response shape + per-layer status table**

```html
    <h2 class="section">One envelope for every failure</h2>
    <p class="lead">Layers 2–4 all return the same <code class="inline">ApiResponse&lt;T&gt;</code> shape — only the <code class="inline">error</code> field differs.</p>
<pre>{
  "successStatus": false,
  "message": "Validation failed",
  "error": "Product price must be greater than zero.",
  "data": null
}</pre>
    <table class="conv">
      <tr><th>Layer</th><th>HTTP</th><th><code>error</code> value</th></tr>
      <tr><td>1 · Route constraint</td><td>404</td><td>— (no body)</td></tr>
      <tr><td>2 · Model binding</td><td>400</td><td>joined ModelState messages</td></tr>
      <tr><td>3 · FluentValidation</td><td>400</td><td>joined validator messages</td></tr>
      <tr><td>4 · Domain invariant</td><td>400</td><td><code class="inline">DOMAIN_RULE_VIOLATION</code></td></tr>
      <tr><td>5 · Persistence</td><td>500</td><td><code class="inline">SERVER_ERROR</code></td></tr>
    </table>
```

- [ ] **Step 6: Add the "Add a validation rule" steps and the footer**

```html
    <h2 class="section">Add a validation rule</h2>
    <ol class="steps">
      <li><b>Decide the layer</b> Use the decision table above to pick where the rule belongs.</li>
      <li><b>Request-shape rule → FluentValidation</b> Add or extend the <code class="inline">AbstractValidator&lt;TRequest&gt;</code> in the module's <code class="inline">.Application/Validators/</code>. The global <code class="inline">ValidationFilter</code> picks it up automatically — no wiring needed.</li>
      <li><b>Entity invariant → Domain guard</b> Add a guard in the entity constructor or method that throws a <code class="inline">DomainException</code> subtype. <code class="inline">ExceptionHandlingMiddleware</code> converts it to a 400 envelope.</li>
      <li><b>Storage integrity → EF constraint</b> Add the constraint in the entity's <code class="inline">IEntityTypeConfiguration&lt;T&gt;</code> and generate a migration.</li>
      <li><b>Run <code class="inline">dotnet test</code></b> Confirm the solution stays green and warning-clean.</li>
    </ol>

    <footer class="page">BaseBackend docs — <a href="ARCHITECTURE_OVERVIEW.html">Architecture Overview</a> · <a href="SOLUTION_STRUCTURE.html">Solution Structure</a> · <a href="PROJECT_LIBRARIES.html">Project Libraries</a> · <a href="REQUEST_FLOW.html">Request Flow</a> · <a href="MODULE_ANATOMY.html">Module Anatomy</a> · <a href="VALIDATION.html">Validation</a></footer>
  </div>
</body>
</html>
```

- [ ] **Step 7: Verify the page is offline-safe and well-formed**

Run: `grep -nE "https?://" docs/VALIDATION.html`
Expected: only matches are inside `xmlns="http://www.w3.org/2000/svg"` (if you added any inline SVG) — otherwise zero matches. No `<link>`/`<script src>`/CDN/web-font.

Run: `grep -c "AutoMapper" docs/VALIDATION.html`
Expected: `0`.

Open `docs/VALIDATION.html` in a browser and confirm the header, banner, legend, flow diagram, five layer cards, both tables, the steps, and the footer all render with the shared styling.

- [ ] **Step 8: Commit**

```bash
git add docs/VALIDATION.html
git commit -m "docs: add VALIDATION.html — five-layer validation reference"
```

---

### Task 3: Folder-by-folder reference in `MODULE_ANATOMY.html`

**Files:**
- Modify: `docs/MODULE_ANATOMY.html` (insert a new section)

**Interfaces:**
- Consumes: the existing `.card`/`.tag`/`table.conv` styles already in the file.

- [ ] **Step 1: Insert the "Folder-by-folder reference" section**

In `docs/MODULE_ANATOMY.html`, find the end of the "The four projects" section — the closing `</div>` of the Contracts card, immediately before this line:
```html
    <h2 class="section">SharedKernel building blocks</h2>
```
Insert the following block **immediately before** that `<h2 class="section">SharedKernel building blocks</h2>` line:

```html
    <h2 class="section">Folder-by-folder reference</h2>
    <p class="lead">What lives in each folder of a module, and the naming convention to follow — taken from the Catalog module.</p>

    <div class="card dom">
      <div class="ch"><span class="tag dom">Domain</span><h3>BaseBackend.Catalog.Domain</h3></div>
      <table class="conv">
        <tr><th>Folder</th><th>What lives here</th><th>Naming convention</th><th>Example</th></tr>
        <tr><td><code class="inline">Entities/</code></td><td>Aggregate roots &amp; entities extending <code class="inline">BaseEntity</code>; one file per aggregate. Business invariants live as guards in their methods.</td><td><code class="inline">&lt;Entity&gt;.cs</code></td><td><code class="inline">Product.cs</code></td></tr>
        <tr><td><code class="inline">Exceptions/</code></td><td>Domain exceptions extending <code class="inline">DomainException</code>, thrown when an invariant is violated.</td><td><code class="inline">Invalid&lt;Entity&gt;Exception.cs</code></td><td><code class="inline">InvalidProductException.cs</code></td></tr>
      </table>
    </div>

    <div class="card app">
      <div class="ch"><span class="tag app">Application</span><h3>BaseBackend.Catalog.Application</h3></div>
      <table class="conv">
        <tr><th>Folder</th><th>What lives here</th><th>Naming convention</th><th>Example</th></tr>
        <tr><td><code class="inline">DTOs/</code></td><td>Request &amp; response records (the transport shape, not the entity).</td><td><code class="inline">&lt;Entity&gt;Dtos.cs</code> · <code class="inline">Create&lt;Entity&gt;Request</code></td><td><code class="inline">ProductDtos.cs</code></td></tr>
        <tr><td><code class="inline">Validators/</code></td><td>One <code class="inline">AbstractValidator&lt;TRequest&gt;</code> per request DTO (FluentValidation).</td><td><code class="inline">&lt;Request&gt;Validator</code></td><td><code class="inline">ProductRequestValidators.cs</code></td></tr>
        <tr><td><code class="inline">Mappings/</code></td><td>Mapperly <code class="inline">[Mapper]</code> partial classes (entity → DTO).</td><td><code class="inline">&lt;Module&gt;Mapper.cs</code></td><td><code class="inline">CatalogMapper.cs</code></td></tr>
        <tr><td><code class="inline">Services/Interfaces/</code></td><td>Use-case service contracts that Infrastructure implements.</td><td><code class="inline">I&lt;Entity&gt;Service.cs</code></td><td><code class="inline">IProductService.cs</code></td></tr>
      </table>
    </div>

    <div class="card inf">
      <div class="ch"><span class="tag inf">Infrastructure</span><h3>BaseBackend.Catalog.Infrastructure</h3></div>
      <table class="conv">
        <tr><th>Folder / file</th><th>What lives here</th><th>Naming convention</th><th>Example</th></tr>
        <tr><td><code class="inline">Data/</code></td><td>The module-scoped EF Core context and its design-time factory.</td><td><code class="inline">&lt;Module&gt;DbContext.cs</code></td><td><code class="inline">CatalogDbContext.cs</code></td></tr>
        <tr><td><code class="inline">Configurations/</code></td><td>One <code class="inline">IEntityTypeConfiguration&lt;T&gt;</code> per entity (Fluent API); applied via <code class="inline">ApplyConfigurationsFromAssembly</code>.</td><td><code class="inline">&lt;Entity&gt;Configuration.cs</code></td><td><code class="inline">ProductConfiguration.cs</code></td></tr>
        <tr><td><code class="inline">Repositories/</code></td><td>Repository interface + EF implementation.</td><td><code class="inline">I&lt;Entity&gt;Repository</code> / <code class="inline">&lt;Entity&gt;Repository</code></td><td><code class="inline">ProductRepository.cs</code></td></tr>
        <tr><td><code class="inline">Services/</code></td><td>Service implementations and the public Contracts facade.</td><td><code class="inline">&lt;Entity&gt;Service.cs</code> · <code class="inline">&lt;Module&gt;ModuleApi.cs</code></td><td><code class="inline">ProductService.cs</code></td></tr>
        <tr><td><code class="inline">Migrations/</code></td><td>EF Core auto-generated migrations &amp; model snapshot — do not hand-edit.</td><td>EF timestamped</td><td><code class="inline">..._InitialCatalog.cs</code></td></tr>
        <tr><td><code class="inline">&lt;Module&gt;Module.cs</code></td><td>Project root: the <code class="inline">IModule</code> entry point that registers everything into DI.</td><td><code class="inline">&lt;Module&gt;Module.cs</code></td><td><code class="inline">CatalogModule.cs</code></td></tr>
      </table>
    </div>

    <div class="card con">
      <div class="ch"><span class="tag con">Contracts</span><h3>BaseBackend.Catalog.Contracts</h3></div>
      <table class="conv">
        <tr><th>Folder / file</th><th>What lives here</th><th>Naming convention</th><th>Example</th></tr>
        <tr><td>(project root)</td><td>The public interface(s) other modules or the host may reference — and nothing else. No subfolders; kept deliberately thin.</td><td><code class="inline">I&lt;Module&gt;Module.cs</code></td><td><code class="inline">ICatalogModule.cs</code></td></tr>
      </table>
    </div>
```

- [ ] **Step 2: Verify the insertion is well-formed and offline-safe**

Run: `grep -c "Folder-by-folder reference" docs/MODULE_ANATOMY.html`
Expected: `1`.

Run: `grep -nE "https?://" docs/MODULE_ANATOMY.html`
Expected: zero matches (no SVG in this file).

Run: `grep -c "AutoMapper" docs/MODULE_ANATOMY.html`
Expected: `0`.

Open `docs/MODULE_ANATOMY.html` in a browser; confirm the new "Folder-by-folder reference" section appears after "The four projects" and before "SharedKernel building blocks", with four cards each containing a folder table.

- [ ] **Step 3: Commit**

```bash
git add docs/MODULE_ANATOMY.html
git commit -m "docs: add folder-by-folder reference to MODULE_ANATOMY"
```

---

### Task 4: Cross-page wiring (footers + REQUEST_FLOW link)

**Files:**
- Modify: `docs/ARCHITECTURE_OVERVIEW.html`, `docs/SOLUTION_STRUCTURE.html`, `docs/PROJECT_LIBRARIES.html`, `docs/REQUEST_FLOW.html`, `docs/MODULE_ANATOMY.html` (footers); `docs/REQUEST_FLOW.html` (validation conventions row)

**Interfaces:**
- Consumes: the new `docs/VALIDATION.html` from Task 2 (its footer already lists all six pages).

- [ ] **Step 1: Add the Validation link to the footer of the five existing pages**

In EACH of `docs/ARCHITECTURE_OVERVIEW.html`, `docs/SOLUTION_STRUCTURE.html`, `docs/PROJECT_LIBRARIES.html`, `docs/REQUEST_FLOW.html`, and `docs/MODULE_ANATOMY.html`, the footer line is currently identical:
```html
    <footer class="page">BaseBackend docs — <a href="ARCHITECTURE_OVERVIEW.html">Architecture Overview</a> · <a href="SOLUTION_STRUCTURE.html">Solution Structure</a> · <a href="PROJECT_LIBRARIES.html">Project Libraries</a> · <a href="REQUEST_FLOW.html">Request Flow</a> · <a href="MODULE_ANATOMY.html">Module Anatomy</a></footer>
```
Replace it in each file with the six-link version (append the Validation link):
```html
    <footer class="page">BaseBackend docs — <a href="ARCHITECTURE_OVERVIEW.html">Architecture Overview</a> · <a href="SOLUTION_STRUCTURE.html">Solution Structure</a> · <a href="PROJECT_LIBRARIES.html">Project Libraries</a> · <a href="REQUEST_FLOW.html">Request Flow</a> · <a href="MODULE_ANATOMY.html">Module Anatomy</a> · <a href="VALIDATION.html">Validation</a></footer>
```

- [ ] **Step 2: Link the deep dive from REQUEST_FLOW's validation conventions row**

In `docs/REQUEST_FLOW.html`, find the validation row's third cell (the file/reference cell), currently:
```html
        <td><code class="inline">BaseBackend.Api/Filters/ValidationFilter.cs</code> · validators in <code class="inline">.Application/Validators/</code></td>
```
Replace it with (append a link to the full reference):
```html
        <td><code class="inline">BaseBackend.Api/Filters/ValidationFilter.cs</code> · validators in <code class="inline">.Application/Validators/</code> · see <a href="VALIDATION.html">Validation</a></td>
```

- [ ] **Step 3: Verify all six pages cross-link and stay offline-safe**

Run: `grep -c "VALIDATION.html" docs/ARCHITECTURE_OVERVIEW.html docs/SOLUTION_STRUCTURE.html docs/PROJECT_LIBRARIES.html docs/MODULE_ANATOMY.html docs/VALIDATION.html`
Expected: each file reports `1` (footer link), except `docs/VALIDATION.html` which reports `1` (its own footer).

Run: `grep -c "VALIDATION.html" docs/REQUEST_FLOW.html`
Expected: `2` (footer link + conventions-row link).

Run: `grep -nE "https?://" docs/*.html`
Expected: only `xmlns="http://www.w3.org/2000/svg"` occurrences (inside inline SVG); no external resources.

- [ ] **Step 4: Commit**

```bash
git add docs/
git commit -m "docs: cross-link VALIDATION.html across all six pages"
```

---

## Self-Review

**Spec coverage** (each spec part → task):
- Part 1 (code fix: `InvalidModelStateResponseFactory` + `ModelStateResponse.From` + `Api.UnitTests`) → Task 1. ✓
- Part 2 (new `VALIDATION.html`: flow diagram, five layer cards with real code, decision table, unified response shape, add-a-rule steps) → Task 2. ✓
- Part 3 (`MODULE_ANATOMY.html` folder-by-folder reference, four layer cards with naming conventions) → Task 3. ✓
- Part 4 (six-page footers + REQUEST_FLOW validation-row link) → Task 4. ✓
- Five-layer model table → rendered in Task 2 (flow + cards + tables). ✓
- Constraints (CPM, net10.0, TreatWarningsAsErrors, offline docs, no AutoMapper, non-generic BaseEntity) → enforced in each task's steps and verifications. ✓

**Placeholder scan:** no TBD/TODO; every code step has complete code; doc steps give exact HTML/snippets and explicit grep verifications; the one "paste the shared style block" instruction names the exact source file to copy from (consistent with how the existing pages were built). ✓

**Type consistency:** `ModelStateResponse.From(ModelStateDictionary) : ApiResponse<object>` is defined in Task 1 Step 4 and consumed in Task 1 Step 6 and referenced in Task 2's card 2. `ApiResponse<object>.Failure(message, error)`, `"Validation failed"`, and `DOMAIN_RULE_VIOLATION` are used consistently across tasks and match the existing codebase. Footer string in Task 4 matches the verified current footer exactly. ✓
