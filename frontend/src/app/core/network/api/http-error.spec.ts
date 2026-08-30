import { HttpErrorResponse } from '@angular/common/http';
import { toAppError } from '@core/network/api/http-error';

describe('toAppError', () => {
  it('maps the backend envelope: message + code from the `error` field', () => {
    const err = new HttpErrorResponse({
      status: 409,
      error: { successStatus: false, message: 'الرقم القومي مستخدم بالفعل', error: 'NID_IN_USE' },
    });
    const app = toAppError(err);
    expect(app.message).toBe('الرقم القومي مستخدم بالفعل');
    expect(app.code).toBe('NID_IN_USE');
    expect(app.status).toBe(409);
    expect(app.kind).toBe('http');
  });

  it('prefers an explicit `code` field when present', () => {
    const err = new HttpErrorResponse({ status: 400, error: { message: 'x', code: 'E_X' } });
    expect(toAppError(err).code).toBe('E_X');
  });

  it('maps status 0 to a network error with no code', () => {
    const app = toAppError(new HttpErrorResponse({ status: 0 }));
    expect(app.kind).toBe('network');
    expect(app.code).toBeUndefined();
  });

  it('captures the backend message into serverMessage', () => {
    const err = new HttpErrorResponse({ status: 409, error: { message: 'مستخدم بالفعل', error: 'NID_IN_USE' } });
    expect(toAppError(err).serverMessage).toBe('مستخدم بالفعل');
  });

  it('leaves serverMessage undefined for a status-0 network error', () => {
    expect(toAppError(new HttpErrorResponse({ status: 0 })).serverMessage).toBeUndefined();
  });

  it('leaves serverMessage undefined when the body has no message (bare 404)', () => {
    expect(toAppError(new HttpErrorResponse({ status: 404 })).serverMessage).toBeUndefined();
  });
});
