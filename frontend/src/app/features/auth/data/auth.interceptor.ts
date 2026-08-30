// authInterceptor: attaches the access token and transparently refreshes once on 401.
import { HttpInterceptorFn, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { from, switchMap, catchError, throwError } from 'rxjs';
import { TokenStore } from '@features/auth/data/token-store';
import { RefreshTokenUseCase } from '@features/auth/domain/usecases/shared/refresh-token.use-case';

const withAuth = (req: HttpRequest<unknown>, token: string | null) =>
  token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

// Auth endpoints must NOT drive the 401→refresh→retry path: a 401 from the refresh
// endpoint would itself be intercepted and trigger another refresh — an infinite loop.
// login/logout 401s have no live session to refresh either. Their 401s propagate as-is.
const AUTH_NO_REFRESH = ['/api/auth/refresh', '/api/auth/login', '/api/auth/logout'];
const bypassesRefresh = (url: string) => AUTH_NO_REFRESH.some((path) => url.includes(path));

export const authInterceptor: HttpInterceptorFn = (req, next: HttpHandlerFn) => {
  const tokens = inject(TokenStore);
  const refresh = inject(RefreshTokenUseCase);
  const router = inject(Router);

  return from(tokens.getAccess()).pipe(
    switchMap((access) =>
      next(withAuth(req, access)).pipe(
        catchError((err) => {
          if (err?.status !== 401 || bypassesRefresh(req.url)) return throwError(() => err);
          // one refresh attempt, then retry the original request
          return from(refresh.run()).pipe(
            switchMap((result) => {
              if (!result.ok) {
                void tokens.clear();
                void router.navigate(['/login']);
                return throwError(() => err);
              }
              return next(withAuth(req, result.data.accessToken));
            }),
          );
        }),
      ),
    ),
  );
};
