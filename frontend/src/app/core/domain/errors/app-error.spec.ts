import { AppError } from '@core/domain/errors/app-error';

describe('AppError', () => {
  it('carries kind, status, and code, and is an Error', () => {
    const e = new AppError('nope', 'auth', 401, 'E_AUTH');
    expect(e).toBeInstanceOf(Error);
    expect(e.name).toBe('AppError');
    expect(e.kind).toBe('auth');
    expect(e.status).toBe(401);
    expect(e.code).toBe('E_AUTH');
  });

  it('carries an optional serverMessage (undefined when omitted)', () => {
    expect(new AppError('x', 'http', 400).serverMessage).toBeUndefined();
    expect(new AppError('x', 'http', 400, 'E', 'من الخادم').serverMessage).toBe('من الخادم');
  });
});
