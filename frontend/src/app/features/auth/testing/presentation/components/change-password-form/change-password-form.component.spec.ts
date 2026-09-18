import { TestBed } from '@angular/core/testing';
import { ChangePasswordForm } from '@features/auth/presentation/components/change-password-form/change-password-form.component';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { TranslateService } from '@core/i18n';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { AuthSession } from '@features/auth/domain/model/shared/auth';

const session: AuthSession = { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'captain', userId: 'U' }, mustChangePassword: false };
const MISMATCH_KEY = 'changePassword.mismatch';
const MISMATCH_MSG = 'New password and confirmation do not match';

describe('ChangePasswordForm', () => {
  const auth = { changePassword: jest.fn() } as unknown as AuthSessionStore;
  const i18n = { t: jest.fn((k: string) => (k === MISMATCH_KEY ? MISMATCH_MSG : k)) } as unknown as TranslateService;
  let cmp: ChangePasswordForm;
  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [ChangePasswordForm, { provide: AuthSessionStore, useValue: auth }, { provide: TranslateService, useValue: i18n }],
    });
    cmp = TestBed.inject(ChangePasswordForm);
  });

  it('blocks submit and shows the mismatch error when confirmation differs', async () => {
    cmp.currentPassword.set('old'); cmp.newPassword.set('new12345'); cmp.confirmPassword.set('different');
    await cmp.submit();
    expect(auth.changePassword).not.toHaveBeenCalled();
    expect(cmp.error()).toBe(MISMATCH_MSG);
  });

  it('calls changePassword, clears fields, and emits succeeded on success', async () => {
    (auth.changePassword as jest.Mock).mockResolvedValue(ok(session));
    const emit = jest.spyOn(cmp.succeeded, 'emit');
    cmp.currentPassword.set('old'); cmp.newPassword.set('new12345'); cmp.confirmPassword.set('new12345');
    await cmp.submit();
    expect(auth.changePassword).toHaveBeenCalledWith('old', 'new12345');
    expect(cmp.error()).toBeNull();
    expect(cmp.currentPassword()).toBe('');
    expect(cmp.newPassword()).toBe('');
    expect(cmp.confirmPassword()).toBe('');
    expect(emit).toHaveBeenCalled();
  });

  it('surfaces the server error and does NOT emit on failure', async () => {
    (auth.changePassword as jest.Mock).mockResolvedValue(fail(new AppError('bad', 'auth', 401, undefined, 'Current password is incorrect')));
    const emit = jest.spyOn(cmp.succeeded, 'emit');
    cmp.currentPassword.set('wrong'); cmp.newPassword.set('new12345'); cmp.confirmPassword.set('new12345');
    await cmp.submit();
    expect(cmp.error()).toBe('Current password is incorrect');
    expect(emit).not.toHaveBeenCalled();
  });
});

describe('ChangePasswordForm — disabled input', () => {
  it('disables the submit button when [disabled] is true (even when not loading)', () => {
    TestBed.configureTestingModule({
      imports: [ChangePasswordForm],
      providers: [{ provide: AuthSessionStore, useValue: { changePassword: jest.fn() } as unknown as AuthSessionStore }],
    });
    const fixture = TestBed.createComponent(ChangePasswordForm);
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(btn.disabled).toBe(true);
  });
});
