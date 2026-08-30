# `_data/<feature>.flows.js` — flow schema reference

Each feature doc renders from a `window.FLOWS` object injected into the shell
(`auth-feature-flows.html`) by `scripts/feature-doc/inject-data.mjs`. This file
documents the **per-flow** shape that powers the drill-down Flows tab
(Actions · Files · Request/Response · Diagram). `auth.flows.js` is the worked
example.

## A flow object

```js
{
  // --- identity / header (existing) ---
  id:"login",                       // unique within the feature; used in the URL
  title:"Login",
  kind:"command",                   // command | query | navigation
  endpointRef:"POST /api/auth/login",
  mapper:"AuthRepository.login",
  optimistic:false,
  gap:true,                         // true -> GAP badge (no backing backend yet)
  errorNote:"…",
  seq:`sequenceDiagram …`,          // Mermaid; NO semicolons in Note lines

  // --- Actions facet: the "path of a request" chain ---
  actions:[
    { actor:"User",                 // optional; only the trigger node sets it
      layer:"trigger",              // trigger | presentation | domain | data | core
      label:"click تسجيل الدخول",
      detail:"loading.set(true)",   // optional short annotation
      file:"login.page.ts" }        // optional; file the step lives in
  ],
  ret:"Result<AuthSession> / AppError — vm navigates to /account",  // return-up line

  // --- Files facet: subset of the feature tree this flow touches ---
  files:[
    { path:"core/auth/usecases/login.use-case.ts",
      role:"use-case",              // page|viewmodel|store|use-case|repository|port|data-source|guard|interceptor|model
      status:"built",               // built | built-not-wired | stub | planned
      note:"" }                     // optional; rendered after the status
  ],

  // --- Request/Response facet ---
  request:  { dto:"LoginRequest",
              fields:[ {name:"email", type:"string", required:true, notes:"…"} ],
              example:`{ "email": "…", "password": "…" }` },
  response: { dto:"SessionDto", status:200,
              fields:[ {name:"accessToken", type:"string", notes:"…"} ],
              example:`{ "accessToken": "…" }`,
              errors:[ "401 … → AppError('auth',401) → vm.error" ] }
}
```

## Rules

- **`request` / `response` are `null`** only for **navigation** flows (guards).
  A command with no response body uses `{ dto:"—", status:204, fields:[], example:"" }`.
- **Enums** (rendered with fixed colors / markers):
  - `layer` → node tint: presentation = purple, domain = green, data = orange,
    core = blue, trigger = neutral.
  - `status` → marker: `built` ✓, `built-not-wired` ⚠ built · not wired,
    `stub` ⚠ stub, `planned` ◌ planned.
- **Grounding:** request/response field lists mirror the matching backend
  blueprint (`docs/backend/<feature>-api-blueprint.html`) so FE and BE agree.
- **Backward compatible:** a flow missing any new field still renders — the
  renderer falls back to the minimal header + mapper line for that facet.
- The files tree is produced by `buildTree(files)` — source of truth and tests
  in `scripts/feature-doc/build-tree.mjs`; an identical copy is inlined in the
  shell (search `keep in sync` there).

## Authoring workflow

1. Edit `docs/frontend/_data/<feature>.flows.js` (never the data block inside the HTML).
2. Sync into the shell:
   `node scripts/feature-doc/inject-data.mjs --file docs/frontend/<feature>-feature-flows.html --match-text "window.FLOWS = {" --data docs/frontend/_data/<feature>.flows.js`
3. Check self-containment: `node scripts/feature-doc/check-doc.mjs docs/frontend/<feature>-feature-flows.html`
4. Open the HTML in a browser and walk each flow's four facets.
