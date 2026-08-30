import { AppError } from '@core/domain/errors/app-error';
import { GENERIC_ERROR_AR, toUserMessage } from '@core/domain/errors/user-message';

describe('toUserMessage', () => {
  it('maps a network error to the generic message', () => {
    expect(toUserMessage(new AppError('Http failure response ...: 0 Unknown Error', 'network')))
      .toBe(GENERIC_ERROR_AR);
  });

  it('maps a 5xx server error to the generic message (even with a serverMessage)', () => {
    expect(toUserMessage(new AppError('boom', 'http', 500, 'SERVER', 'خطأ من الخادم')))
      .toBe(GENERIC_ERROR_AR);
  });

  it.each(['validation', 'storage', 'crypto', 'unknown'] as const)(
    'maps client-side kind "%s" to the generic message',
    (kind) => {
      expect(toUserMessage(new AppError('technical detail', kind))).toBe(GENERIC_ERROR_AR);
    },
  );

  it('passes a backend message through for a 4xx with serverMessage', () => {
    expect(toUserMessage(new AppError('البريد مستخدم', 'http', 409, 'EMAIL_IN_USE', 'البريد مستخدم')))
      .toBe('البريد مستخدم');
  });

  it('passes a backend message through for a 401 auth error with serverMessage', () => {
    expect(toUserMessage(new AppError('بيانات غير صحيحة', 'auth', 401, undefined, 'بيانات غير صحيحة')))
      .toBe('بيانات غير صحيحة');
  });

  it('maps a 4xx WITHOUT a serverMessage (bare 404) to the generic message', () => {
    expect(toUserMessage(new AppError('HTTP 404', 'http', 404))).toBe(GENERIC_ERROR_AR);
  });
});
