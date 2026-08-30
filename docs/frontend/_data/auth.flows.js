window.FLOWS = {
  meta: { title:"Kheprx — Auth Feature Flows", subtitle:"Login · sessions · roles · passwords · users — decisions in docs/decisions/auth-decisions.html (AD-001…014)" },
  legend: {
    actionKinds: [
      { key: "query", label: "Query (read)", desc: "Fetches data from the backend; maps the response into a facade signal." },
      { key: "command", label: "Command (write)", desc: "Mutates server state; on success updates the facade signal." },
      { key: "local", label: "Local", desc: "Component-only state (form binds, filters, modal open/close); no network." },
      { key: "navigation", label: "Navigation", desc: "Router navigation only; no data action." }
    ],
    methods: [
      { key: "GET", desc: "Read a resource or collection." },
      { key: "POST", desc: "Create a resource or invoke an action." },
      { key: "PATCH", desc: "Partially update a resource." },
      { key: "PUT", desc: "Replace a resource." },
      { key: "DELETE", desc: "Remove a resource." }
    ],
    gap: "The action has no backing endpoint in the API blueprint; the row shows a proposed signature the backend must add.",
    optimistic: "The command updates the local signal before the server response returns.",
    status: "Planned · P1 = decided & in the Phase 1 plan; Deferred · P2 = postponed to Phase 2 (user management)."
  },
  canonicalFlow: `sequenceDiagram
  actor U as User
  participant P as Page (smart)
  participant F as Facade (signals)
  participant D as Data service
  participant A as ApiService (+interceptors)
  participant B as Backend endpoint
  U->>P: click action
  P->>F: action(args)
  F->>F: loading() = true
  F->>D: call(RequestDto)
  D->>A: METHOD /api/...
  A->>B: HTTP (base-url, auth header)
  B-->>A: 200 ResponseDto
  A-->>D: ApiResponse<ResponseDto>
  D->>D: toModel(dto)
  D-->>F: Model
  F->>F: signal.update(...), loading() = false
  F-->>P: signals change -> re-render
  Note over A,B: error -> interceptor -> AppError(kind) -> F.error() -> toast`,

  gapRegister: [
    {id:"FE-AUTH-BE", phase:"p1", screens:"Login, Account", feature:"auth", module:"identity", type:"missing backend", direction:"All /api/auth/* calls hit a backend that does not exist yet; the app runs on MockAuthDataSource. See backend AUTH-01..08.", severity:"blocker"},
    {id:"FE-AUTH-ROLE", phase:"p1", screens:"Login, Admin", feature:"auth", module:"identity", type:"role model mismatch", direction:"[AD-008 DECIDED] Extend the frontend UserRole union from 'admin'|'user' to admin|manager|moqawel|worker; update the mock seeds + roleGuard usages, as part of the Mock→Http switch.", severity:"important"},
    {id:"FE-AUTH-WIRE", phase:"p1", screens:"Login", feature:"auth", module:"identity", type:"wiring", direction:"[AD-001 token storage = localStorage + Bearer, refresh in body — unchanged] AUTH_DATA_SOURCE defaults to MockAuthDataSource; swap to HttpAuthDataSource + set env.api.baseUrl when live. Logout now signs out everywhere (AD-009); login returns mustChangePassword → redirect to change-password (AD-007).", severity:"important"},
    {id:"FE-AUTH-PWD", phase:"p1", screens:"Change password", feature:"auth", module:"identity", type:"missing screen+usecase", direction:"No change-password route, use-case, or page exists; build it against /api/auth/change-password.", severity:"important"},
    {id:"FE-AUTH-USR", phase:"p2", screens:"Admin → Users", feature:"auth", module:"identity", type:"stub UI", direction:"AdminPage is a static 'Admin area' stub; build a user list + create/edit/role-assign wired to /api/users.", severity:"important"},
    {id:"FE-AUTH-ME", phase:"p1", screens:"(rehydrate)", feature:"auth", module:"identity", type:"shortcut", direction:"AuthSessionStore.rehydrate() parses the mock token shape instead of calling GET /api/auth/me; call /me once the backend exists.", severity:"minor"}
  ],

  features: [{
    id:"auth", name:"Auth", color:"--f-auth",
    businessFlow:"The user signs in on the Login page; LoginViewModel.submit() calls AuthSessionStore.signIn() → LoginUseCase → AuthRepository → AUTH_DATA_SOURCE (Mock today, Http when live). Tokens are persisted by TokenStore; authInterceptor attaches the Bearer token and refreshes once on 401. authGuard protects /account; roleGuard('admin') protects /admin. Logout, password flows, and user management complete the feature. [Decisions AD-001/007/008/009: token storage = localStorage + Bearer + refresh in body; logout signs out everywhere; login surfaces mustChangePassword (forced first-login change); UserRole extends to the 4 roles.]",
    folderTree: `features/auth/
├─ presentation/
│  ├─ pages/login.page.ts        (built)
│  ├─ pages/account.page.ts      (built — shows principal + signOut)
│  ├─ pages/admin.page.ts        (STUB — target: Users management)
│  └─ viewmodels/login.viewmodel.ts (built)
core/auth/
├─ auth.types.ts                 (UserRole='admin'|'user' → extend to 4)
├─ auth.repository.ts / .interface.ts / .port.ts (built)
├─ auth-session.store.ts         (signIn/signOut + role/principal signals)
├─ token-store.ts                (save/getAccess/getRefresh/clear)
├─ auth-data-source.ts           (port: login/refresh)
├─ http-auth.data-source.ts      (built, NOT wired)
├─ mock-auth.data-source.ts      (DEFAULT; 2 demo users)
├─ auth.providers.ts             (AUTH_DATA_SOURCE → Mock)
└─ usecases/{login,refresh-token,logout}.use-case.ts (built)
core/guards/auth.guard.ts        (authGuard + roleGuard — built)
core/network/interceptors/auth.interceptor.ts (Bearer + 401→refresh→retry — built)
PLANNED: usecases/change-password.use-case.ts, change-password page + users`,

    endpoints:[
      {method:"POST", path:"/api/auth/login", schema:"identity", reqDto:"LoginRequest", resDto:"SessionDto", gap:true, note:"Planned · P1 — backend Identity + FE Mock→Http; see auth-session-lifecycle spec/plan. (FE-AUTH-BE)"},
      {method:"POST", path:"/api/auth/refresh", schema:"identity", reqDto:"RefreshRequest", resDto:"SessionDto", gap:true, note:"Planned · P1 — RefreshTokenUseCase via authInterceptor 401 path. (FE-AUTH-BE/WIRE)"},
      {method:"POST", path:"/api/auth/logout", schema:"identity", reqDto:"RefreshRequest", resDto:"—", gap:true, note:"Planned · P1 — server revoke-all (AD-009); clears tokens locally today. (FE-AUTH-BE)"},
      {method:"GET",  path:"/api/auth/me", schema:"identity", reqDto:"—", resDto:"CurrentUserDto", gap:true, note:"Planned · P1 — call /me once backend exists; rehydrate parses token today. (FE-AUTH-ME)"},
      {method:"POST", path:"/api/auth/change-password", schema:"identity", reqDto:"ChangePasswordRequest", resDto:"—", gap:true, note:"Planned · P1 (AD-007 forced first-login) — FE screen+usecase to build. (FE-AUTH-PWD)"},
      {method:"GET",  path:"/api/users", schema:"identity", reqDto:"—", resDto:"PagedResult<UserDto>", gap:true, note:"Deferred · P2 — admin users list; UI is a stub today. (FE-AUTH-USR)"},
      {method:"POST", path:"/api/users", schema:"identity", reqDto:"CreateUserRequest", resDto:"UserDto", gap:true, note:"Deferred · P2 — admin create user. (FE-AUTH-USR)"}
    ],

    flows:[
      {id:"login", title:"Login", kind:"command", endpointRef:"POST /api/auth/login", reqDto:"LoginRequest", resDto:"SessionDto", mapper:"AuthRepository.login", optimistic:false, gap:true,
       actions:[
         {actor:"User", layer:"trigger", label:"click تسجيل الدخول"},
         {layer:"presentation", label:"LoginPage → vm.submit()", file:"login.page.ts"},
         {layer:"presentation", label:"LoginViewModel → store.signIn()", detail:"loading.set(true)", file:"login.viewmodel.ts"},
         {layer:"domain", label:"LoginUseCase.run()", detail:"validate + TokenStore.save", file:"login.use-case.ts"},
         {layer:"data", label:"AuthRepository.login()", detail:"toModel(dto)", file:"auth.repository.ts"},
         {layer:"data", label:"AUTH_DATA_SOURCE — Mock today / Http live", file:"mock-auth.data-source.ts"},
         {layer:"core", label:"POST /api/auth/login", file:"core/network"}],
       ret:"Result<AuthSession> / AppError — vm navigates to /account",
       files:[
         {path:"features/auth/presentation/pages/login.page.ts", role:"page", status:"built"},
         {path:"features/auth/presentation/viewmodels/login.viewmodel.ts", role:"viewmodel", status:"built"},
         {path:"core/auth/auth-session.store.ts", role:"store", status:"built"},
         {path:"core/auth/usecases/login.use-case.ts", role:"use-case", status:"built"},
         {path:"core/auth/auth.repository.ts", role:"repository", status:"built"},
         {path:"core/auth/auth-data-source.ts", role:"port", status:"built"},
         {path:"core/auth/mock-auth.data-source.ts", role:"data-source", status:"built", note:"default"},
         {path:"core/auth/http-auth.data-source.ts", role:"data-source", status:"built-not-wired"},
         {path:"core/auth/token-store.ts", role:"store", status:"built"}],
       request:{dto:"LoginRequest", fields:[
         {name:"email", type:"string", required:true, notes:"login identifier"},
         {name:"password", type:"string", required:true, notes:"plaintext over TLS"}],
         example:`{ "email": "admin@example.com", "password": "••••••" }`},
       response:{dto:"SessionDto", status:200, fields:[
         {name:"accessToken", type:"string", notes:"JWT, ~15 min"},
         {name:"refreshToken", type:"string", notes:"rotated each refresh"},
         {name:"principal", type:"object", notes:"{ id, role }"},
         {name:"mustChangePassword", type:"boolean", notes:"AD-007 forced first-login"}],
         example:`{ "accessToken": "eyJ…", "refreshToken": "r-8f2…",\n  "principal": { "id": 1, "role": "admin" },\n  "mustChangePassword": false }`,
         errors:["401 invalid credentials → AppError('auth',401) → vm.error on the login form"]},
       errorNote:"invalid credentials → AppError('auth',401) → vm.error set → message shown on the login form",
       seq:`sequenceDiagram
  actor U as User
  participant P as LoginPage
  participant V as LoginViewModel
  participant S as AuthSessionStore
  participant UC as LoginUseCase
  participant R as AuthRepository
  participant DS as AUTH_DATA_SOURCE
  participant B as Auth API
  U->>P: type email+password, click تسجيل الدخول
  P->>V: submit()
  V->>V: loading.set(true)
  V->>S: signIn(email, password)
  S->>UC: run({email,password})
  UC->>R: login(email,password)
  R->>DS: login(email,password)
  DS->>B: POST /api/auth/login  [GAP: backend missing]
  B-->>DS: 200 AuthSession {tokens, principal}
  DS-->>R: AuthSession
  R-->>UC: AuthSession
  UC->>UC: TokenStore.save(session.tokens)
  UC-->>S: Result.ok(AuthSession)
  S->>S: _session.set(session)
  S-->>V: Result.ok
  V->>V: router.navigate(['/account'])
  Note over DS,B: today DS = MockAuthDataSource (2 demo users) — 401 → AppError(auth) → vm.error`},

      {id:"refresh", title:"Token refresh (on 401)", kind:"command", endpointRef:"POST /api/auth/refresh", reqDto:"RefreshRequest", resDto:"SessionDto", mapper:"AuthRepository.refresh", optimistic:false, gap:true,
       actions:[
         {layer:"trigger", label:"Any HTTP request → 401 Unauthorized"},
         {layer:"core", label:"authInterceptor catches 401", file:"auth.interceptor.ts"},
         {layer:"domain", label:"RefreshTokenUseCase.run()", file:"refresh-token.use-case.ts"},
         {layer:"data", label:"AuthRepository.refresh(refreshToken)", file:"auth.repository.ts"},
         {layer:"data", label:"AUTH_DATA_SOURCE.refresh()", file:"mock-auth.data-source.ts"},
         {layer:"core", label:"POST /api/auth/refresh", file:"core/network"}],
       ret:"new tokens → TokenStore.save → retry original request with new Bearer",
       files:[
         {path:"core/network/interceptors/auth.interceptor.ts", role:"interceptor", status:"built"},
         {path:"core/auth/usecases/refresh-token.use-case.ts", role:"use-case", status:"built"},
         {path:"core/auth/auth.repository.ts", role:"repository", status:"built"},
         {path:"core/auth/auth-data-source.ts", role:"port", status:"built"},
         {path:"core/auth/mock-auth.data-source.ts", role:"data-source", status:"built", note:"default"},
         {path:"core/auth/http-auth.data-source.ts", role:"data-source", status:"built-not-wired"},
         {path:"core/auth/token-store.ts", role:"store", status:"built"}],
       request:{dto:"RefreshRequest", fields:[
         {name:"refreshToken", type:"string", required:true, notes:"current refresh token from TokenStore"}],
         example:`{ "refreshToken": "r-8f2…" }`},
       response:{dto:"SessionDto", status:200, fields:[
         {name:"accessToken", type:"string", notes:"new JWT"},
         {name:"refreshToken", type:"string", notes:"rotated"}],
         example:`{ "accessToken": "eyJ…(new)", "refreshToken": "r-9a1…(rotated)" }`,
         errors:["refresh fails → TokenStore.clear() → navigate('/login')"]},
       errorNote:"refresh fails → TokenStore.clear() → navigate(['/login'])",
       seq:`sequenceDiagram
  participant Req as Any HTTP request
  participant I as authInterceptor
  participant UC as RefreshTokenUseCase
  participant R as AuthRepository
  participant DS as AUTH_DATA_SOURCE
  participant B as Auth API
  Req->>I: 401 Unauthorized
  I->>UC: run()
  UC->>R: refresh(refreshToken from TokenStore)
  R->>DS: refresh(refreshToken)
  DS->>B: POST /api/auth/refresh  [GAP]
  B-->>DS: 200 AuthTokens
  DS-->>UC: AuthTokens
  UC->>UC: TokenStore.save(tokens)
  UC-->>I: ok
  I->>Req: retry original request with new Bearer
  Note over I,B: on failure → TokenStore.clear() + navigate('/login')`},

      {id:"logout", title:"Logout", kind:"command", endpointRef:"POST /api/auth/logout", reqDto:"RefreshRequest", resDto:"—", mapper:"AuthSessionStore.signOut", optimistic:true, gap:true,
       actions:[
         {actor:"User", layer:"trigger", label:"click Sign out"},
         {layer:"presentation", label:"AccountPage → store.signOut()", file:"account.page.ts"},
         {layer:"presentation", label:"AuthSessionStore.signOut()", file:"auth-session.store.ts"},
         {layer:"domain", label:"LogoutUseCase.run()", detail:"TokenStore.clear()", file:"logout.use-case.ts"},
         {layer:"core", label:"POST /api/auth/logout (revoke — target)", file:"core/network"}],
       ret:"_session.set(null) → navigate('/login') · local clear always succeeds",
       files:[
         {path:"features/auth/presentation/pages/account.page.ts", role:"page", status:"built"},
         {path:"core/auth/auth-session.store.ts", role:"store", status:"built"},
         {path:"core/auth/usecases/logout.use-case.ts", role:"use-case", status:"built"},
         {path:"core/auth/token-store.ts", role:"store", status:"built"},
         {path:"core/auth/http-auth.data-source.ts", role:"data-source", status:"planned", note:"server revoke (target)"}],
       request:{dto:"RefreshRequest", fields:[
         {name:"refreshToken", type:"string", required:true, notes:"token to revoke server-side (target)"}],
         example:`{ "refreshToken": "r-8f2…" }`},
       response:{dto:"—", status:204, fields:[], example:"",
         errors:["none — local clear always succeeds; server revoke is best-effort"]},
       errorNote:"none — local clear always succeeds; server revoke is best-effort",
       seq:`sequenceDiagram
  actor U as User
  participant P as AccountPage
  participant S as AuthSessionStore
  participant UC as LogoutUseCase
  U->>P: click Sign out
  P->>S: signOut()
  S->>UC: run()
  UC->>UC: TokenStore.clear()
  UC-->>S: ok
  S->>S: _session.set(null)
  S-->>P: done → navigate('/login')
  Note over UC: target adds POST /api/auth/logout to revoke the server refresh token [GAP]`},

      {id:"guard", title:"Route guards", kind:"navigation", endpointRef:"—", reqDto:"—", resDto:"—", mapper:"authGuard / roleGuard", optimistic:false, gap:false,
       actions:[
         {layer:"trigger", label:"Router.canActivate(/account or /admin)"},
         {layer:"core", label:"authGuard / roleGuard('admin')", file:"auth.guard.ts"},
         {layer:"core", label:"AuthSessionStore.isAuthenticated() / role()", file:"auth-session.store.ts"}],
       ret:"allowed → true · unauthenticated → /login · wrong role → /",
       files:[
         {path:"core/guards/auth.guard.ts", role:"guard", status:"built", note:"authGuard + roleGuard"},
         {path:"core/auth/auth-session.store.ts", role:"store", status:"built"}],
       request:null,
       response:null,
       errorNote:"unauthenticated → /login; wrong role → /",
       seq:`sequenceDiagram
  participant Router
  participant G as authGuard / roleGuard
  participant S as AuthSessionStore
  Router->>G: canActivate(/account or /admin)
  G->>S: isAuthenticated() / role()
  alt not authenticated
    G-->>Router: false → navigate('/login')
  else wrong role
    G-->>Router: false → navigate('/')
  else allowed
    G-->>Router: true
  end
  Note over G,S: roleGuard('admin') on /admin today — extend to manager/moqawel/worker (FE-AUTH-ROLE)`},

      {id:"change-password", title:"Change password", kind:"command", endpointRef:"POST /api/auth/change-password", reqDto:"ChangePasswordRequest", resDto:"—", mapper:"(planned) ChangePasswordUseCase", optimistic:false, gap:true,
       actions:[
         {actor:"User", layer:"trigger", label:"enter current + new password, submit"},
         {layer:"presentation", label:"ChangePasswordPage → vm.submit()", file:"change-password.page.ts"},
         {layer:"presentation", label:"ChangePasswordViewModel", file:"change-password.viewmodel.ts"},
         {layer:"domain", label:"ChangePasswordUseCase.run()", file:"change-password.use-case.ts"},
         {layer:"data", label:"AUTH_DATA_SOURCE.changePassword()", file:"http-auth.data-source.ts"},
         {layer:"core", label:"POST /api/auth/change-password", file:"core/network"}],
       ret:"200 ok → success toast → redirect",
       files:[
         {path:"features/auth/presentation/pages/change-password.page.ts", role:"page", status:"planned"},
         {path:"features/auth/presentation/viewmodels/change-password.viewmodel.ts", role:"viewmodel", status:"planned"},
         {path:"core/auth/usecases/change-password.use-case.ts", role:"use-case", status:"planned"},
         {path:"core/auth/auth-data-source.ts", role:"port", status:"planned", note:"add changePassword()"}],
       request:{dto:"ChangePasswordRequest", fields:[
         {name:"currentPassword", type:"string", required:true, notes:"verified before change"},
         {name:"newPassword", type:"string", required:true, notes:"min length per policy"}],
         example:`{ "currentPassword": "••••••", "newPassword": "••••••••" }`},
       response:{dto:"—", status:200, fields:[], example:"",
         errors:["wrong current password → AppError('auth',400) → form error"]},
       errorNote:"wrong current password → AppError('auth',400) → form error",
       seq:`sequenceDiagram
  actor U as User
  participant P as ChangePasswordPage [PLANNED]
  participant V as ChangePasswordViewModel [PLANNED]
  participant UC as ChangePasswordUseCase [PLANNED]
  participant DS as AUTH_DATA_SOURCE
  participant B as Auth API
  U->>P: current + new password, submit
  P->>V: submit()
  V->>UC: run({currentPassword,newPassword})
  UC->>DS: changePassword(...)
  DS->>B: POST /api/auth/change-password  [GAP]
  B-->>DS: 200 ok
  DS-->>V: ok → success toast
  Note over P,B: entire column is FE-AUTH-PWD (not built)`},

      {id:"list-users", title:"List users (Admin)", kind:"query", endpointRef:"GET /api/users", reqDto:"—", resDto:"PagedResult<UserDto>", mapper:"(planned) UsersRepository.list", optimistic:false, gap:true,
       errorNote:"403 if not admin → roleGuard redirect",
       actions:[
         {layer:"trigger", label:"AdminPage loads / paginates"},
         {layer:"presentation", label:"UsersViewModel.load()", file:"users.viewmodel.ts"},
         {layer:"domain", label:"ListUsersUseCase.run()", file:"list-users.use-case.ts"},
         {layer:"data", label:"UsersRepository.list(page)", file:"users.repository.ts"},
         {layer:"core", label:"GET /api/users?page=…", file:"core/network"}],
       ret:"PagedResult<UserDto> → users signal",
       files:[
         {path:"features/auth/presentation/pages/admin.page.ts", role:"page", status:"stub", note:"target: users management"},
         {path:"features/auth/presentation/viewmodels/users.viewmodel.ts", role:"viewmodel", status:"planned"},
         {path:"core/auth/usecases/list-users.use-case.ts", role:"use-case", status:"planned"},
         {path:"core/auth/users.repository.ts", role:"repository", status:"planned"}],
       request:{dto:"(query string)", fields:[
         {name:"page", type:"number", required:false, notes:"1-based, default 1"},
         {name:"pageSize", type:"number", required:false, notes:"default 20"}],
         example:`GET /api/users?page=1&pageSize=20`},
       response:{dto:"PagedResult<UserDto>", status:200, fields:[
         {name:"items", type:"UserDto[]", notes:"id, email, role, status"},
         {name:"page", type:"number", notes:"current page"},
         {name:"pageSize", type:"number", notes:"page size"},
         {name:"total", type:"number", notes:"total rows"}],
         example:`{ "items": [ { "id": 1, "email": "admin@example.com", "role": "admin", "status": "active" } ],\n  "page": 1, "pageSize": 20, "total": 1 }`,
         errors:["403 not admin → roleGuard redirect to /"]},
       seq:`sequenceDiagram
  actor A as Admin
  participant P as AdminPage [STUB]
  participant V as UsersViewModel [PLANNED]
  participant UC as ListUsersUseCase [PLANNED]
  participant B as Users API
  A->>P: open /admin
  P->>V: load()
  V->>UC: run({page})
  UC->>B: GET /api/users  [GAP FE-AUTH-USR]
  B-->>UC: 200 PagedResult<UserDto>
  UC-->>V: users
  Note over P,B: AdminPage is a static stub today (FE-AUTH-USR)`},

      {id:"create-user", title:"Create user (Admin)", kind:"command", endpointRef:"POST /api/users", reqDto:"CreateUserRequest", resDto:"UserDto", mapper:"(planned) UsersRepository.create", optimistic:false, gap:true,
       errorNote:"409 email exists → AppError('conflict') → form error",
       actions:[
         {actor:"Admin", layer:"trigger", label:"click New user, submit form"},
         {layer:"presentation", label:"UsersViewModel.create()", file:"users.viewmodel.ts"},
         {layer:"domain", label:"CreateUserUseCase.run()", file:"create-user.use-case.ts"},
         {layer:"data", label:"UsersRepository.create(dto)", file:"users.repository.ts"},
         {layer:"core", label:"POST /api/users", file:"core/network"}],
       ret:"UserDto → append to users signal",
       files:[
         {path:"features/auth/presentation/pages/admin.page.ts", role:"page", status:"stub"},
         {path:"features/auth/presentation/viewmodels/users.viewmodel.ts", role:"viewmodel", status:"planned"},
         {path:"core/auth/usecases/create-user.use-case.ts", role:"use-case", status:"planned"},
         {path:"core/auth/users.repository.ts", role:"repository", status:"planned"}],
       request:{dto:"CreateUserRequest", fields:[
         {name:"email", type:"string", required:true, notes:"unique"},
         {name:"role", type:"string", required:true, notes:"admin|manager|moqawel|worker"},
         {name:"fullName", type:"string", required:false, notes:"display name"}],
         example:`{ "email": "new@example.com", "role": "worker", "fullName": "New User" }`},
       response:{dto:"UserDto", status:201, fields:[
         {name:"id", type:"number", notes:"new id"},
         {name:"email", type:"string", notes:"echoed"},
         {name:"role", type:"string", notes:"one of the 4 roles"},
         {name:"status", type:"string", notes:"active|disabled"},
         {name:"mustChangePassword", type:"boolean", notes:"true for first login"}],
         example:`{ "id": 5, "email": "new@example.com", "role": "worker",\n  "status": "active", "mustChangePassword": true }`,
         errors:["409 email exists → AppError('conflict') → form error"]},
       seq:`sequenceDiagram
  actor A as Admin
  participant V as UsersViewModel [PLANNED]
  participant UC as CreateUserUseCase [PLANNED]
  participant B as Users API
  A->>V: submit New user form
  V->>UC: run(CreateUserRequest)
  UC->>B: POST /api/users  [GAP FE-AUTH-USR]
  B-->>UC: 201 UserDto
  UC-->>V: user → append to list
  Note over V,B: entire flow is FE-AUTH-USR (not built)`},
    ],

    screens:[
      {name:"Login", route:"/login", guard:"redirect-if-authed",
       actions:[
         {label:"Submit login", element:"(click) تسجيل الدخول button / Enter", componentMethod:"vm.submit()", serviceAction:"AuthSessionStore.signIn(email, password)", kind:"command", flowRef:"login"}],
       data:[
         {element:"email input", storeField:"LoginViewModel.email signal", backendCol:"identity.Users.Email", status:"⚠ FE-AUTH-BE"},
         {element:"password input", storeField:"LoginViewModel.password signal", backendCol:"identity.Users.PasswordHash", status:"⚠ FE-AUTH-BE"},
         {element:"error banner", storeField:"LoginViewModel.error signal", backendCol:"— (AppError message)", status:"✓"},
         {element:"demo users hint", storeField:"static template text", backendCol:"MockAuthDataSource.USERS (admin@example.com, user@example.com)", status:"⚠ FE-AUTH-ROLE"}]},
      {name:"Account", route:"/account", guard:"authGuard",
       actions:[
         {label:"Sign out", element:"(click) Sign out", componentMethod:"signOut()", serviceAction:"AuthSessionStore.signOut()", kind:"command", flowRef:"logout"}],
       data:[
         {element:"role / userId display", storeField:"AuthSessionStore.principal signal", backendCol:"identity.Users.RoleId / Id", status:"✓"}]},
      {name:"Admin → Users", route:"/admin", guard:"roleGuard('admin')",
       actions:[
         {label:"List users", element:"users table", componentMethod:"(planned) load()", serviceAction:"GET /api/users", kind:"query", flowRef:"list-users"},
         {label:"Create user", element:"(click) New user", componentMethod:"(planned) create()", serviceAction:"POST /api/users", kind:"command", flowRef:"create-user"}],
       data:[
         {element:"users list", storeField:"(planned) users signal", backendCol:"identity.Users + identity.Roles", status:"⚠ FE-AUTH-USR"}]},
      {name:"Change password", route:"/change-password (planned)", guard:"authGuard",
       actions:[
         {label:"Change own password", element:"change form", componentMethod:"(planned)", serviceAction:"POST /api/auth/change-password", kind:"command", flowRef:"change-password"}],
       data:[
         {element:"change-password form", storeField:"(planned viewmodel)", backendCol:"identity.Users.PasswordHash", status:"⚠ FE-AUTH-PWD"}]}
    ]
  }]
};
