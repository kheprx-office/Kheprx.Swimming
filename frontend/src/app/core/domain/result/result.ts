// Result: the uniform success/failure value returned by every use case via the
// UseCase base. Callers switch on `ok` instead of writing try/catch themselves.
import { AppError } from '@core/domain/errors/app-error';

export type Result<T> =
  | { ok: true; data: T }
  | { ok: false; error: AppError };

export const ok = <T>(data: T): Result<T> => ({ ok: true, data });
export const fail = (error: AppError): Result<never> => ({ ok: false, error });
