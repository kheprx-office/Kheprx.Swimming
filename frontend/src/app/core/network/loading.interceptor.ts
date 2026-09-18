// loadingInterceptor: increments the LoadingService counter around every request and
// decrements it via finalize (covers success, error, and cancel — no leaks). Silent auth
// calls (transparent token refresh, logout) opt out so background traffic never blips the UI.
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { LoadingService } from './loading.service';

const SILENT = ['/api/auth/refresh', '/api/auth/logout'];
const isSilent = (url: string) => SILENT.some((path) => url.includes(path));

export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  if (isSilent(req.url)) return next(req);
  const loading = inject(LoadingService);
  loading.begin();
  return next(req).pipe(finalize(() => loading.end()));
};
