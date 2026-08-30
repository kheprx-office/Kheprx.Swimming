const SCHEMA = [
 {schema:"identity",color:"--identity",tables:[
  {name:"Users",note:"canonical person + login identity (owned by the Identity module). Login is OPTIONAL — null Email/PasswordHash = a login-less person (e.g. field worker). Role-specific profiles (Manager/Moqawel/Worker) attach via UserId in a later People/HR feature. [G5: unified identity]",cols:[
   {c:"Id",t:"uuid",k:"PK",n:false,d:"gen_random_uuid()"},
   {c:"Email",t:"text",k:"U",n:true,note:"login email (unique); null for login-less persons"},
   {c:"PasswordHash",t:"text",k:"",n:true,note:"PBKDF2 hash; null when the person has no login"},
   {c:"FullName",t:"text",k:"",n:false},
   {c:"Phone",t:"text",k:"",n:true},
   {c:"RoleId",t:"uuid",k:"FK",fk:"identity.Roles.Id",n:false},
   {c:"IsActive",t:"boolean",k:"",n:false,d:"true"},
   {c:"MustChangePassword",t:"boolean",k:"",n:false,d:"false",note:"seeded accounts start true → force change on first login [G7]"},
   {c:"LastLoginAt",t:"timestamptz",k:"",n:true},
   {c:"CreatedAt",t:"timestamptz",k:"",n:false,d:"now()"},
   {c:"UpdatedAt",t:"timestamptz",k:"",n:false,d:"now()"}]},
  {name:"Roles",note:"lookup — admin | manager | moqawel | worker",cols:[
   {c:"Id",t:"uuid",k:"PK",n:false,d:"gen_random_uuid()"},
   {c:"Code",t:"text",k:"U",n:false,note:"admin | manager | moqawel | worker"},
   {c:"LabelAr",t:"text",k:"",n:true},
   {c:"LabelEn",t:"text",k:"",n:true},
   {c:"SortOrder",t:"int",k:"",n:false,d:"0"},
   {c:"IsActive",t:"boolean",k:"",n:false,d:"true"}]},
  {name:"RefreshTokens",note:"rotating refresh tokens for JWT renewal",cols:[
   {c:"Id",t:"uuid",k:"PK",n:false,d:"gen_random_uuid()"},
   {c:"UserId",t:"uuid",k:"FK",fk:"identity.Users.Id",n:false},
   {c:"TokenHash",t:"text",k:"U",n:false,note:"SHA-256 of the opaque refresh token"},
   {c:"ExpiresAt",t:"timestamptz",k:"",n:false},
   {c:"CreatedAt",t:"timestamptz",k:"",n:false,d:"now()"},
   {c:"RevokedAt",t:"timestamptz",k:"",n:true},
   {c:"ReplacedByTokenHash",t:"text",k:"",n:true,note:"set on rotation"}]}
 ]}
];

const RELATIONSHIPS = [
 {from:"identity.Roles",to:"identity.Users",card:"1-N",label:"has"},
 {from:"identity.Users",to:"identity.RefreshTokens",card:"1-N",label:"issues"}
];

const API = [
 { module:"Identity", schema:"identity", purpose:"Authentication, JWT sessions, roles, and admin user management.",
   resources:[
     {name:"Auth", entity:"Auth", role:"transactional",
       endpoints:[
         {m:"POST", p:"/api/auth/login", s:"Email + password → access + refresh JWT", req:"LoginRequest", res:"SessionDto", auth:["anonymous"], codes:[200,400,401]},
         {m:"POST", p:"/api/auth/refresh", s:"Rotate refresh token → new tokens", req:"RefreshRequest", res:"SessionDto", auth:["anonymous"], codes:[200,401]},
         {m:"POST", p:"/api/auth/logout", s:"Revoke ALL the caller's refresh tokens — sign out everywhere [G9]", req:"—", res:"—", auth:["any"], codes:[200,401]},
         {m:"GET",  p:"/api/auth/me", s:"Current user + role from JWT", res:"CurrentUserDto", auth:["any"], codes:[200,401]},
         {m:"POST", p:"/api/auth/change-password", s:"Change own password (old → new)", req:"ChangePasswordRequest", res:"—", auth:["any"], codes:[200,400,401]}],
       dtos:[
         {name:"LoginRequest", kind:"request", fields:[
           {f:"email",t:"string",rules:"NotEmpty; EmailAddress"},
           {f:"password",t:"string",rules:"NotEmpty"}]},
         {name:"RefreshRequest", kind:"request", fields:[
           {f:"refreshToken",t:"string",rules:"NotEmpty"}]},
         {name:"ChangePasswordRequest", kind:"request", fields:[
           {f:"currentPassword",t:"string",rules:"NotEmpty"},
           {f:"newPassword",t:"string",rules:"NotEmpty; MinLength(8)"}]},
         {name:"SessionDto", kind:"response", fields:[
           {f:"accessToken",t:"string",rules:"short-lived JWT (e.g. 15 min)"},
           {f:"refreshToken",t:"string",rules:"opaque; rotated on refresh"},
           {f:"role",t:"string",rules:"admin | manager | moqawel | worker"},
           {f:"userId",t:"Guid",rules:"identity.Users.Id"},
           {f:"mustChangePassword",t:"bool",rules:"true → client must redirect to change-password [G7]"}]},
         {name:"CurrentUserDto", kind:"response", fields:[
           {f:"userId",t:"Guid",rules:""},
           {f:"email",t:"string",rules:""},
           {f:"fullName",t:"string",rules:""},
           {f:"role",t:"string",rules:""}]}],
       files:[
         ["src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Auth/IAuthService.cs","login/refresh/logout/me/password service interface"],
         ["src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Auth/AuthService.cs","credential check, JWT issue, refresh rotation"],
         ["src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Auth/Dtos.cs","LoginRequest/RefreshRequest/SessionDto/CurrentUserDto/…"],
         ["src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Auth/Validators.cs","FluentValidation for the request DTOs"],
         ["src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Auth/JwtTokenService.cs","JWT issue + validate; signing key from config"],
         ["src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Auth/PasswordHasher.cs","PBKDF2 hash + verify"],
         ["Kheprx.BaseBackend.Api/Controllers/AuthController.cs","api/auth/* endpoints (ApiResponse<T>)"]],
       wiring:`// IdentityModuleRegistration.cs — Auth is a service over Users/RefreshTokens (no Auth entity)
services.AddScoped<IAuthService, AuthService>();
services.AddScoped<IJwtTokenService, JwtTokenService>();
services.AddScoped<IPasswordHasher, PasswordHasher>();

// Program.cs — JWT bearer auth + role policies
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(/* signing key from .NET user-secrets / env — never committed (AD secrets) */);
builder.Services.AddAuthorization();`,
     },
     {name:"Users", entity:"User", role:"master",
       endpoints:[
         {m:"GET",  p:"/api/users", s:"List/filter users (paged)", res:"PagedResult<UserDto>", auth:["admin"], codes:[200,401,403]},
         {m:"GET",  p:"/api/users/{id}", s:"Get user by id", res:"UserDto", auth:["admin"], codes:[200,404]},
         {m:"POST", p:"/api/users", s:"Create user (role + temp password)", req:"CreateUserRequest", res:"UserDto", auth:["admin"], codes:[200,400]},
         {m:"PUT",  p:"/api/users/{id}", s:"Update user (name/phone/active)", req:"UpdateUserRequest", res:"UserDto", auth:["admin"], codes:[200,400,404]},
         {m:"PUT",  p:"/api/users/{id}/role", s:"Assign role", req:"AssignRoleRequest", res:"UserDto", auth:["admin"], codes:[200,400,404]},
         {m:"DELETE",p:"/api/users/{id}", s:"Deactivate / soft-delete user", auth:["admin"], codes:[200,404,409]}],
       dtos:[
         {name:"UserDto", kind:"response", fields:[
           {f:"id",t:"Guid",rules:""},
           {f:"email",t:"string",rules:""},
           {f:"fullName",t:"string",rules:""},
           {f:"phone",t:"string?",rules:""},
           {f:"role",t:"string",rules:"admin | manager | moqawel | worker"},
           {f:"isActive",t:"bool",rules:""}]},
         {name:"CreateUserRequest", kind:"request", fields:[
           {f:"email",t:"string",rules:"NotEmpty; EmailAddress"},
           {f:"fullName",t:"string",rules:"NotEmpty; MaxLength(200)"},
           {f:"phone",t:"string?",rules:""},
           {f:"roleId",t:"Guid",rules:"NotEmpty; exists in identity.Roles"},
           {f:"temporaryPassword",t:"string",rules:"NotEmpty; MinLength(8)"}]},
         {name:"UpdateUserRequest", kind:"request", fields:[
           {f:"fullName",t:"string",rules:"NotEmpty; MaxLength(200)"},
           {f:"phone",t:"string?",rules:""},
           {f:"isActive",t:"bool",rules:""}]},
         {name:"AssignRoleRequest", kind:"request", fields:[
           {f:"roleId",t:"Guid",rules:"NotEmpty; exists in identity.Roles"}]}],
     },
     {name:"Roles", entity:"Role", role:"lookup",
       endpoints:[
         {m:"GET", p:"/api/roles", s:"List roles (?activeOnly)", res:"List<RoleDto>", auth:["any"], codes:[200]}]
     }
   ]}
];

const FEATURES = [
 {key:"login", en:"Login & Sessions", ar:"تسجيل الدخول", color:"--identity",
  flow:"[Phase 1] A user signs in with email + password; the API verifies the hash, issues a short-lived access JWT plus a rotating refresh token, and records LastLoginAt.",
  resources:["Identity.Auth","Identity.Users","Identity.Roles"],
  gaps:[
   {id:"AUTH-01", phase:"p1", type:"missing endpoint", severity:"blocker", direction:"implement POST /api/auth/login issuing access + refresh JWT (backend currently has NO authentication of any kind)"},
   {id:"AUTH-02", phase:"p1", type:"missing module", severity:"blocker", direction:"create the Identity module (Domain/Application/Contracts/Infrastructure) by copying the Catalog module; note Catalog's BaseEntity uses int Id while the Identity schema uses Guid PKs — give the Identity entities Guid keys rather than inheriting the int BaseEntity"},
   {id:"AUTH-03", phase:"p1", type:"missing table", severity:"important", direction:"add identity.RefreshTokens with rotation columns (TokenHash, ExpiresAt, RevokedAt, ReplacedByTokenHash)"}]},
 {key:"refresh-logout", en:"Refresh & Logout", ar:"تجديد الجلسة والخروج", color:"--identity",
  flow:"[Phase 1] Before the access token expires the client exchanges its refresh token for a new pair; logout revokes the refresh token server-side.",
  resources:["Identity.Auth"],
  gaps:[
   {id:"AUTH-04", phase:"p1", type:"missing endpoint", severity:"important", direction:"add POST /api/auth/refresh (rotate) and POST /api/auth/logout (revoke) backed by identity.RefreshTokens"}]},
 {key:"authorization", en:"Roles & Authorization", ar:"الصلاحيات", color:"--identity",
  flow:"[Phase 1] Every endpoint declares which of the four roles may call it; the host validates the JWT and enforces role policies.",
  resources:["Identity.Roles","Identity.Users","Identity.Auth"],
  gaps:[
   {id:"AUTH-05", phase:"p1", type:"missing config", severity:"blocker", direction:"wire AddAuthentication(JwtBearer) + AddAuthorization policies in the API host; add [Authorize] to controllers"},
   {id:"AUTH-06", phase:"p1", type:"data/seed", severity:"important", direction:"seed identity.Roles with admin/manager/moqawel/worker and seed an initial admin user"}]},
 {key:"passwords", en:"Password Management", ar:"كلمات المرور", color:"--identity",
  flow:"[Phase 1] Change-own-password — seeded accounts must change their password on first login (G7).",
  resources:["Identity.Auth"],
  gaps:[
   {id:"AUTH-07", phase:"p1", type:"missing endpoint", severity:"important", direction:"add the change-password endpoint (authenticated; satisfies G7 forced first-login)"}]},
 {key:"user-management", en:"User Management", ar:"إدارة المستخدمين", color:"--identity",
  flow:"[Phase 2] An admin lists, creates, updates, deactivates users and assigns each a role.",
  resources:["Identity.Users","Identity.Roles"],
  gaps:[
   {id:"AUTH-08", phase:"p2", type:"missing endpoint", severity:"important", direction:"add /api/users CRUD + PUT /api/users/{id}/role, admin-only"}]}
];
