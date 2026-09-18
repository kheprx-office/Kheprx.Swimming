# Relocate blood_type from swimmer_profile to medical_exam — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the implemented `blood_type_id` from `identity.swimmer_profile` (backend + Register Swimmer form) and relocate it to `athlete.medical_exam` in the schema docs.

**Architecture:** A removal refactor. `blood_type_id` is dropped from the `SwimmerProfile` entity/config/DTO/validator/service/seeder (with a DROP-column migration) and from the Register Swimmer frontend (viewmodel/form/dto/i18n). The `blood_type` reference lookup + `GET /api/reference/blood-types` + `LoadBloodTypesUseCase` are **kept** (unused now, for the future `medical_exam` consumer). The `medical_exam` side is docs-only (that table is unimplemented).

**Tech Stack:** .NET 10 / EF Core / FluentValidation / xUnit + Moq (backend); Angular 20 standalone + signals / Jest (frontend).

**Spec:** `docs/superpowers/specs/2026-09-18-relocate-blood-type-design.md`

## Global Constraints

- This is a **removal**: intermediate states may not compile until all references in a layer are removed. Each task's edits land together, then build + tests verify. (TDD here = update tests to the new contract + confirm green, not red→green.)
- **Keep the blood_type reference lookup slice** — do NOT touch `BloodType` entity/config/repository, `ReferenceService.GetBloodTypesAsync`, `ReferenceController` `blood-types`, `IdentityModuleExtensions` DI of `IBloodTypeRepository`, `IdentitySeeder.EnsureBloodTypes`, or the frontend `LoadBloodTypesUseCase` / `getBloodTypes` / reference DTO. They stay unused for the future `medical_exam`.
- **DROP the column** — no data preservation. `swimmer_profile.blood_type_id` is nullable demo data, re-seeded.
- `CreateSwimmerRequest` is a **positional record**; removing `BloodTypeId` (6th positional, after `Dob`) shifts positions — every positional construction site must drop that argument.
- **DLL lock:** stop the backend dev server before `dotnet ef` / `dotnet test`.
- **Solution file** is `backend/Kheprx.BaseBackend.sln`.
- **Frontend** has no `lint` script — verify with `npx jest <path>` + `npm run build`.
- **Commits:** each task ends with a commit step per plan format; defer/skip if holding work uncommitted until the user asks.

**Paths:** Identity module `backend/src/Modules/Identity/`; Identity tests `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/`; Api tests `backend/tests/Kheprx.BaseBackend.Api.UnitTests/`; frontend `frontend/src/app/`.

---

## Task 1: Backend — remove blood_type from swimmer_profile

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/SwimmerProfile.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/SwimmerProfileConfiguration.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/SwimmerDtos.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Validators/UserRequestValidators.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/SwimmerMessages.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentitySeeder.cs`
- Modify: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/SwimmerProfileTests.cs`
- Modify: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs`
- Modify: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Validators/CreateSwimmerRequestValidatorTests.cs`
- Modify: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs`
- Generated: `.../Identity.Infrastructure/Migrations/<timestamp>_DropSwimmerBloodType.cs` (+ snapshot)

**Interfaces:**
- Produces: `SwimmerProfile(Guid userId, string uid, Guid trainingClubId, Guid? representChampionshipClubId = null)` (no bloodTypeId); `CreateSwimmerRequest(string NameEn, string Username, Guid TrainingClubId, Guid GenderId, DateOnly Dob, IReadOnlyList<Guid> StrokeIds, string? NameAr, string? Email, string? Phone, Guid? RepresentChampionshipClubId)` (no BloodTypeId).

- [ ] **Step 1: Entity — remove BloodTypeId**

In `SwimmerProfile.cs`: delete the property `public Guid? BloodTypeId { get; private set; }`, the ctor parameter `Guid? bloodTypeId = null` (make `representChampionshipClubId` the last param), and the assignment `BloodTypeId = bloodTypeId;`. The ctor becomes:

```csharp
    public SwimmerProfile(Guid userId, string uid, Guid trainingClubId,
        Guid? representChampionshipClubId = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Uid = uid.Trim();
        TrainingClubId = trainingClubId;
        RepresentChampionshipClubId = representChampionshipClubId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }
```

- [ ] **Step 2: EF config — remove the FK**

In `SwimmerProfileConfiguration.cs`, delete the line:

```csharp
        builder.HasOne<BloodType>().WithMany().HasForeignKey(s => s.BloodTypeId).OnDelete(DeleteBehavior.Restrict);
```

(Leave the `AppUser` and two `Club` relationships intact.)

- [ ] **Step 3: DTO — remove BloodTypeId from CreateSwimmerRequest**

In `SwimmerDtos.cs`, remove the `Guid BloodTypeId,` line so the record is:

```csharp
public sealed record CreateSwimmerRequest(
    string NameEn,
    string Username,
    Guid TrainingClubId,
    Guid GenderId,
    DateOnly Dob,
    IReadOnlyList<Guid> StrokeIds,
    string? NameAr,
    string? Email,
    string? Phone,
    Guid? RepresentChampionshipClubId);
```

- [ ] **Step 4: Validator — remove the blood-type rule**

In `UserRequestValidators.cs`, inside `CreateSwimmerRequestValidator`, delete:

```csharp
        RuleFor(x => x.BloodTypeId)
            .NotEqual(Guid.Empty).WithMessage(_ => SwimmerMessages.Errors.BloodTypeRequired(AppLanguage.Current));
```

- [ ] **Step 5: Service — drop the blood-type dependency + check + ctor arg**

In `SwimmerService.cs`:
- Remove the field `private readonly IBloodTypeRepository _bloodTypes;`.
- Remove `IBloodTypeRepository bloodTypes,` from the constructor parameters and the assignment `_bloodTypes = bloodTypes;`. The constructor becomes:

```csharp
    public SwimmerService(
        ISwimmerProfileRepository swimmers, IUserRepository users, IRoleRepository roles,
        IClubRepository clubs, IStrokeRepository strokes,
        IGenderRepository genders, IPasswordHasher hasher, IOptions<AccountCreationOptions> options)
    {
        _swimmers = swimmers;
        _users = users;
        _roles = roles;
        _clubs = clubs;
        _strokes = strokes;
        _genders = genders;
        _hasher = hasher;
        _options = options.Value;
    }
```

- In `EnsureReferencesExist`, delete the line:

```csharp
        if (!await _bloodTypes.ExistsAsync(r.BloodTypeId, ct)) throw new InvalidUserException("Unknown blood type.");
```

- In `CreateAsync`, drop the `request.BloodTypeId` argument when constructing the profile:

```csharp
        var profile = new SwimmerProfile(user.Id, uid, request.TrainingClubId,
            request.RepresentChampionshipClubId);
```

- [ ] **Step 6: Messages — remove BloodTypeRequired**

In `SwimmerMessages.cs`, delete:

```csharp
        public static string BloodTypeRequired(string lang) => lang switch { "ar" => "فصيلة الدم مطلوبة", _ => "Blood type is required" };
```

- [ ] **Step 7: Seeder — seed swimmers without blood type**

In `IdentitySeeder.cs`, inside `EnsureSwimmers`:
- Delete the line `var bloodId = await db.BloodTypes.OrderBy(b => b.Code).Select(b => b.Id).FirstAsync(ct);`.
- Change the profile construction to drop `bloodTypeId`:

```csharp
            var profile = new SwimmerProfile(user.Id, $"SW-{i:D4}", clubId);
```

(Do NOT touch `EnsureBloodTypes` — the lookup seed stays.)

- [ ] **Step 8: Update SwimmerProfileTests**

In `SwimmerProfileTests.cs`, in `Ctor_assigns_id_uid_fks_and_timestamps`: delete `var bloodId = Guid.NewGuid();`, change the construction to `var p = new SwimmerProfile(userId, " SW-0007 ", clubId);`, and delete `Assert.Equal(bloodId, p.BloodTypeId);`.

- [ ] **Step 9: Update SwimmerServiceTests**

In `SwimmerServiceTests.cs`:
- `Req()`: drop the 6th positional (the blood-type Guid):

```csharp
    private static CreateSwimmerRequest Req() => new(
        "Mona Ali", "mona.ali", Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2010, 5, 1),
        new[] { Guid.NewGuid() }, null, null, null, null);
```

- `Build()`: delete the line `var blood = new Mock<IBloodTypeRepository>(); ...`, and remove `blood.Object,` from the `new SwimmerService(...)` call so it reads:

```csharp
        var svc = new SwimmerService(swimmers.Object, users.Object, roles.Object,
            clubs.Object, strokes.Object, genders.Object, hasher.Object, opts);
```

- [ ] **Step 10: Update CreateSwimmerRequestValidatorTests**

In `CreateSwimmerRequestValidatorTests.cs`:
- `Valid()`: remove `BloodTypeId: Guid.NewGuid(),`:

```csharp
    private static CreateSwimmerRequest Valid() => new(
        NameEn: "Mona Ali", Username: "mona.ali", TrainingClubId: Guid.NewGuid(),
        GenderId: Guid.NewGuid(), Dob: new DateOnly(2010, 5, 1),
        StrokeIds: new[] { Guid.NewGuid() }, NameAr: null, Email: null, Phone: null,
        RepresentChampionshipClubId: null);
```

- In `Missing_required_fields_fail`, delete the line `Assert.False(_v.Validate(Valid() with { BloodTypeId = Guid.Empty }).IsValid);`.

- [ ] **Step 11: Update SwimmersControllerTests**

In `SwimmersControllerTests.cs`, both `CreateSwimmerRequest(...)` fixtures drop the 6th positional (the `Guid.NewGuid()` after `new DateOnly(2010, 5, 1)`), becoming:

```csharp
        var req = new CreateSwimmerRequest("Mona Ali", "mona.ali", Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2010, 5, 1), new[] { Guid.NewGuid() }, null, null, null, null);
```

(There are two occurrences — in `Create_returns_201_with_created_swimmer` and `Create_returns_409_when_service_returns_null`. Update both.)

- [ ] **Step 12: Generate the DROP-column migration**

Stop the dev server first. From the repo root:

```bash
dotnet ef migrations add DropSwimmerBloodType \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api \
  --context IdentityDbContext
```

Verify the `Up()` drops the FK constraint, the index, and the `BloodTypeId` column on `swimmer_profile` (EF names them `FK_swimmer_profile_blood_type_BloodTypeId` / `IX_swimmer_profile_BloodTypeId` / `BloodTypeId`), and `Down()` re-adds them. Confirm the snapshot updated. Quote the `Up()` in your report.

- [ ] **Step 13: Build + run the backend suites**

Run:
```bash
dotnet build backend/Kheprx.BaseBackend.sln
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests
dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests
dotnet test backend/tests/Kheprx.BaseBackend.ArchitectureTests
```
Expected: build succeeds; all suites green. (No compile references to `BloodTypeId` remain in swimmer code.)

- [ ] **Step 14: Commit**

```bash
git add backend/src/Modules/Identity/ backend/tests/
git commit -m "refactor(identity): remove blood_type from swimmer_profile + registration"
```

---

## Task 2: Frontend — remove blood_type from Register Swimmer

**Files:**
- Modify: `frontend/src/app/features/captain-panel/presentation/pages/account-creation/register-swimmer.viewmodel.ts`
- Modify: `frontend/src/app/features/captain-panel/presentation/pages/account-creation/register-swimmer-form.component.html`
- Modify: `frontend/src/app/features/swimmers/data/dto/create-swimmer.dto.ts`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`
- Modify: `frontend/src/app/features/captain-panel/testing/presentation/pages/account-creation/register-swimmer.viewmodel.spec.ts`
- Modify: `frontend/src/app/features/swimmers/testing/domain/usecases/create-swimmer.use-case.spec.ts`
- Modify: `frontend/src/app/features/swimmers/testing/data/repositories/swimmer.repository.impl.spec.ts`

**Interfaces:**
- Consumes: nothing new. Produces the `CreateSwimmerDtoRq` shape without `bloodTypeId` (matches the backend contract from Task 1).

- [ ] **Step 1: Request DTO — remove bloodTypeId**

In `create-swimmer.dto.ts`, delete `bloodTypeId: string;` from `CreateSwimmerDtoRq`.

- [ ] **Step 2: Viewmodel — remove all blood-type wiring**

In `register-swimmer.viewmodel.ts`:
- Import (line 2): remove `LoadBloodTypesUseCase` → `import { LoadClubsUseCase, LoadGendersUseCase, LoadStrokesUseCase } from '@features/reference';`.
- Delete `private readonly loadBloodTypes = inject(LoadBloodTypesUseCase);`.
- Delete `readonly bloodTypes = signal<LookupItem[]>([]);`.
- Delete `readonly bloodTypeId = signal('');`.
- In `canSubmit`, delete the clause `this.bloodTypeId().length > 0 &&`.
- Rewrite `loadLookups` to drop blood types:

```typescript
  private async loadLookups(): Promise<void> {
    const [c, g, s] = await Promise.all([
      this.loadClubs.run(), this.loadGenders.run(), this.loadStrokes.run(),
    ]);
    if (c.ok) this.clubs.set(c.data);
    if (g.ok) this.genders.set(g.data);
    if (s.ok) this.strokes.set(s.data);
  }
```

- In `submit`, delete `bloodTypeId: this.bloodTypeId(),` from the `createSwimmer.run({...})` payload.
- In `reset`, remove `this.bloodTypeId` from the signal-clearing array.

- [ ] **Step 3: Form template — remove the Blood Type field**

In `register-swimmer-form.component.html`, delete the line:

```html
    <app-select-field [label]="'accountCreation.fields.bloodType' | translate" [placeholder]="'accountCreation.placeholders.selectOne' | translate" [options]="vm.bloodTypes()" [value]="vm.bloodTypeId()" (valueChange)="vm.bloodTypeId.set($event)"></app-select-field>
```

- [ ] **Step 4: i18n — remove the bloodType label (both locales)**

In `frontend/src/app/core/i18n/en.json` and `ar.json`, remove the `accountCreation.fields.bloodType` key (en value `"Blood Type"`; ar value the Arabic equivalent). Leave `accountCreation.placeholders.selectOne` — it's still used by other selects. Ensure both files remain valid JSON (no trailing comma left behind).

- [ ] **Step 5: Update the viewmodel spec**

In `register-swimmer.viewmodel.spec.ts`:
- Import (line 5): remove `LoadBloodTypesUseCase`.
- Delete the provider `{ provide: LoadBloodTypesUseCase, useValue: lookups() },`.
- In the two submit tests, delete `vm.bloodTypeId.set('b1');` (it appears in `creates and exposes...` and `surfaces a conflict error`).

- [ ] **Step 6: Update the swimmer request-fixture specs**

- In `create-swimmer.use-case.spec.ts`, remove `bloodTypeId: 'b1', ` from the `rq` fixture.
- In `swimmer.repository.impl.spec.ts`, remove `bloodTypeId: 'b1', ` from the `rq` fixture.

- [ ] **Step 7: Run the affected jest suites + build**

Run (from `frontend/`):
```bash
npx jest src/app/features/captain-panel src/app/features/swimmers
npm run build
```
Expected: the register-swimmer + swimmer specs pass; `ng build` succeeds (compiles the trimmed template + validates en/ar JSON).

- [ ] **Step 8: Commit**

```bash
git add frontend/src/app/features/ frontend/src/app/core/i18n/en.json frontend/src/app/core/i18n/ar.json
git commit -m "refactor(account-creation): drop the Blood Type field from Register Swimmer"
```

---

## Task 3: Docs — relocate blood_type to medical_exam

**Files:**
- Modify: `docs/references/swimming-database-diagram.html`
- Modify: `docs/superpowers/specs/2026-08-30-swimming-database-design.md`

**Interfaces:** none (documentation only).

- [ ] **Step 1: Diagram — remove blood_type_id from swimmer_profile**

In `docs/references/swimming-database-diagram.html`, in the `swimmer_profile` table, delete the line:

```javascript
   {c:"blood_type_id",t:"uuid",k:"FK",fk:"reference.blood_type.id",n:true},
```

(It sits between `represent_championship_club_id` and `created_at`.)

- [ ] **Step 2: Diagram — add blood_type_id to medical_exam**

In the `medical_exam` table, add the column after `spine_assess_id`:

```javascript
   {c:"spine_assess_id",t:"uuid",k:"FK",fk:"reference.fitness_assessment.id",n:false},
   {c:"blood_type_id",t:"uuid",k:"FK",fk:"reference.blood_type.id",n:true},
   {c:"hemoglobin",t:"numeric(4,1)",k:"",n:false},
```

- [ ] **Step 3: Diagram — re-point the blood_type relationship**

Change the relationship line from:

```javascript
 {from:"reference.blood_type",to:"identity.swimmer_profile",card:"1-N",label:"of"}
```

to:

```javascript
 {from:"reference.blood_type",to:"athlete.medical_exam",card:"1-N",label:"typed in"}
```

- [ ] **Step 4: Spec — move the blood_type_id row**

In `docs/superpowers/specs/2026-08-30-swimming-database-design.md`:
- In §5 `swimmer_profile`, delete the row `| blood_type_id | uuid | FK → reference.blood_type; nullable |`.
- In §6 `medical_exam`, add a row after `spine_assess`:

```
| spine_assess_id | uuid | FK → reference.fitness_assessment |
| blood_type_id | uuid | FK → reference.blood_type (nullable) |
```

- In §11, change `blood_type 1—* swimmer_profile` to `blood_type 1—* medical_exam` (in the lookups relationship line).

- [ ] **Step 5: Verify the diagram parses + data is coherent**

Run:
```bash
node -e '
const fs=require("fs");
const html=fs.readFileSync("docs/references/swimming-database-diagram.html","utf8");
const appdata=html.match(/<script id="appdata">([\s\S]*?)<\/script>/)[1];
eval(appdata + "\nglobalThis.__S=SCHEMA;globalThis.__R=RELATIONSHIPS;");
const S=globalThis.__S,R=globalThis.__R;
const tables={}; S.forEach(s=>s.tables.forEach(t=>tables[s.schema+"."+t.name]=t));
const sp=tables["identity.swimmer_profile"].cols.map(c=>c.c);
const me=tables["athlete.medical_exam"].cols.map(c=>c.c);
console.log("swimmer_profile has blood_type_id:", sp.includes("blood_type_id"), "(expect false)");
console.log("medical_exam has blood_type_id:", me.includes("blood_type_id"), "(expect true)");
console.log("blood_type->medical_exam rel:", R.some(r=>r.from==="reference.blood_type"&&r.to==="athlete.medical_exam"));
console.log("blood_type->swimmer_profile rel gone:", !R.some(r=>r.to==="identity.swimmer_profile"&&r.from==="reference.blood_type"));
let bad=[]; S.forEach(s=>s.tables.forEach(t=>t.cols.forEach(c=>{ if(c.fk){ const [sc,tb]=c.fk.split("."); if(!(sc+"."+tb in tables)) bad.push(c.fk); } })));
console.log("unresolved FK targets:", bad.length? [...new Set(bad)] : "none");
'
```
Expected: swimmer_profile has blood_type_id **false**; medical_exam **true**; the new relationship present; the old one gone; no unresolved FK targets.

- [ ] **Step 6: Commit**

```bash
git add docs/references/swimming-database-diagram.html docs/superpowers/specs/2026-08-30-swimming-database-design.md
git commit -m "docs: relocate blood_type_id from swimmer_profile to medical_exam"
```

---

## Self-Review Notes (author)

- **Spec coverage:** §3 backend removal → Task 1 (entity/config/DTO/validator/service/messages/seeder/migration + tests); §4 frontend removal → Task 2 (viewmodel/form/dto/i18n + specs); §5 docs relocation → Task 3; §7 testing → the updated backend + frontend suites in Tasks 1–2 and the diagram-parse check in Task 3. §2.2 keep-the-lookup honored (no edits to the blood_type lookup slice in any task). §8 out-of-scope respected. Note: `IdentitySeederTests` needs **no** change — its `Assert.Equal(8, ... BloodTypes.CountAsync())` is the lookup seed, which stays (so it is intentionally absent from Task 1's file list).
- **Type consistency:** `SwimmerProfile` ctor loses `bloodTypeId` (updated at all 3 call-sites: service, seeder, entity test). `CreateSwimmerRequest` loses `BloodTypeId` (positional) — updated at every construction site: `SwimmerServiceTests.Req()`, `SwimmersControllerTests` (×2), and the named-arg `CreateSwimmerRequestValidatorTests.Valid()`. Frontend `CreateSwimmerDtoRq` loses `bloodTypeId`, matched by the viewmodel payload + both request-fixture specs. `IBloodTypeRepository` removed from `SwimmerService` ctor (and its test `Build`), but kept in DI + `ReferenceService`.
- **Removal ordering:** Task 1's edits are interdependent (the entity/DTO changes break call-sites until all are updated) — they land together, and Step 13 build+test is the gate. Migration is generated (Step 12) after the entity/config edits so EF sees the delta.
