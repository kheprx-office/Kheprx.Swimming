import { AppError } from '@core/domain/errors/app-error';
import { ok, fail, Result } from '@core/domain/result/result';

describe('Result', () => {
  it('ok() wraps data with ok=true', () => {
    const r: Result<number> = ok(42);
    expect(r).toEqual({ ok: true, data: 42 });
  });

  it('fail() wraps an AppError with ok=false', () => {
    const err = new AppError('boom', 'network');
    const r = fail(err);
    expect(r.ok).toBe(false);
    if (!r.ok) {
      expect(r.error).toBe(err);
      expect(r.error.kind).toBe('network');
    }
  });

  it('AppError carries kind and optional status', () => {
    const err = new AppError('HTTP 404', 'http', 404);
    expect(err).toBeInstanceOf(Error);
    expect(err.name).toBe('AppError');
    expect(err.kind).toBe('http');
    expect(err.status).toBe(404);
  });
});
