// toAppError: maps an HttpErrorResponse (or transport error) to a typed AppError,
// parsing the backend error body ({ message?, code?, error? }) when present.
import { HttpErrorResponse } from '@angular/common/http';
import { AppError } from '@core/domain/errors/app-error';

export function toAppError(err: unknown): AppError {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) {
      return new AppError(err.message || 'Network request failed', 'network');
    }
    const body = err.error as { message?: string; code?: string; error?: string } | string | null;
    const serverMessage =
      body && typeof body === 'object' && body.message ? body.message : undefined;
    const message = serverMessage ?? `HTTP ${err.status}`;
    const code =
      body && typeof body === 'object' ? (body.code ?? body.error ?? undefined) : undefined;
    const kind = err.status === 401 ? 'auth' : 'http';
    return new AppError(message, kind, err.status, code, serverMessage);
  }
  // rxjs TimeoutError or any other transport failure
  return new AppError('Network request failed', 'network');
}
