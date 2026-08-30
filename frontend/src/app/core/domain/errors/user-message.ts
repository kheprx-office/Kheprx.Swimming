// toUserMessage: the single boundary that decides what an AppError shows the *user*.
// Technical / transport / server errors collapse to one friendly Arabic message; a
// meaningful message the backend itself sent (validation/business/auth) passes through.
// AppError.message is untouched, so logs keep full technical detail.
import { AppError } from '@core/domain/errors/app-error';

export const GENERIC_ERROR_AR = 'حدث خطأ في النظام، حاول مرة أخرى لاحقًا';

export function toUserMessage(e: AppError): string {
  // Server crash → generic, regardless of any body message.
  if (e.status != null && e.status >= 500) return GENERIC_ERROR_AR;
  // Only genuine backend responses (http/auth under 500) may carry a message the
  // user should see; every other kind is technical → generic. Framing it as an
  // allow-list means any future AppErrorKind defaults safely to generic.
  if (e.kind === 'http' || e.kind === 'auth') return e.serverMessage ?? GENERIC_ERROR_AR;
  return GENERIC_ERROR_AR;
}
