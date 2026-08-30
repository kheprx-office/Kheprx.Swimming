// AuthSessionStore: the single source of truth for the current auth session, exposed
// as signals for guards and pages. Wraps the login/logout/change-password use cases and
// rehydrates via LoadCurrentUserUseCase (GET /api/auth/me) on construction so real JWTs
// are validated server-side.
import { Injectable, computed, inject, signal } from '@angular/core';
import { LoginUseCase } from '@features/auth/domain/usecases/login/login.use-case';
import { LogoutUseCase } from '@features/auth/domain/usecases/account/logout.use-case';
import { ChangePasswordUseCase } from '@features/auth/domain/usecases/change-password/change-password.use-case';
import { LoadCurrentUserUseCase } from '@features/auth/domain/usecases/shared/load-current-user.use-case';
import { TokenStore } from '@features/auth/data/token-store';
import { AuthSession } from '@features/auth/domain/model/shared/auth';
import { UserRole } from '@core/domain/roles';
import { Result } from '@core/domain/result/result';

@Injectable({ providedIn: 'root' })
export class AuthSessionStore {
  private readonly loginUseCase = inject(LoginUseCase);
  private readonly logoutUseCase = inject(LogoutUseCase);
  private readonly changePasswordUseCase = inject(ChangePasswordUseCase);
  private readonly loadCurrentUser = inject(LoadCurrentUserUseCase);
  private readonly tokens = inject(TokenStore);

  private readonly _session = signal<AuthSession | null>(null);
  readonly session = this._session.asReadonly();
  readonly isAuthenticated = computed(() => this._session() !== null);
  readonly principal = computed(() => this._session()?.principal ?? null);
  readonly role = computed<UserRole | null>(() => this._session()?.principal.role ?? null);
  readonly mustChangePassword = computed<boolean>(() => this._session()?.mustChangePassword ?? false);

  private readonly _fullName = signal<string | null>(null);
  /** The signed-in user's display name (from GET /api/auth/me), or null until loaded. */
  readonly fullName = this._fullName.asReadonly();
  /** Display name with graceful fallback: the full name, else the userId, else ''. */
  readonly currentUserName = computed<string>(() => this._fullName() ?? this.principal()?.userId ?? '');

  // Rehydration is kicked off eagerly on construction; the promise is captured so an
  // APP_INITIALIZER can await whenReady() and route guards never run before the session
  // is restored. A failed rehydrate must never block bootstrap — degrade to
  // unauthenticated (→ /login), so swallow rejections here.
  private readonly rehydrated: Promise<void> = this.rehydrate().catch(() => undefined);

  /** Resolves once the startup session-rehydration (GET /api/auth/me) has settled,
   *  whether it restored a session or not. Awaited by an APP_INITIALIZER. */
  whenReady(): Promise<void> {
    return this.rehydrated;
  }

  async signIn(email: string, password: string): Promise<Result<AuthSession>> {
    const r = await this.loginUseCase.run({ email, password });
    if (r.ok) {
      this._session.set(r.data);
      await this.refreshProfileName();
    }
    return r;
  }

  async signOut(): Promise<void> {
    await this.logoutUseCase.run();
    this._session.set(null);
    this._fullName.set(null);
  }

  async changePassword(currentPassword: string, newPassword: string): Promise<Result<AuthSession>> {
    const r = await this.changePasswordUseCase.run({ currentPassword, newPassword });
    if (r.ok) {
      this._session.set(r.data);
      await this.refreshProfileName();
    }
    return r;
  }

  private async rehydrate(): Promise<void> {
    const accessToken = await this.tokens.getAccess();
    const refreshToken = await this.tokens.getRefresh();
    if (!accessToken || !refreshToken) return;
    const r = await this.loadCurrentUser.run();
    if (r.ok) {
      this._session.set({ tokens: { accessToken, refreshToken }, principal: { role: r.data.role, userId: r.data.userId }, mustChangePassword: false });
      this._fullName.set(r.data.fullName);
    }
    // invalid/expired → authInterceptor handles 401→refresh→retry, or the user re-logs in.
  }

  /** Best-effort load of the signed-in user's display name via GET /api/auth/me.
   *  Never throws: a failed fetch leaves the name null and consumers fall back to the userId. */
  private async refreshProfileName(): Promise<void> {
    try {
      const r = await this.loadCurrentUser.run();
      if (r.ok) this._fullName.set(r.data.fullName);
    } catch {
      // best-effort — ignore
    }
  }
}
