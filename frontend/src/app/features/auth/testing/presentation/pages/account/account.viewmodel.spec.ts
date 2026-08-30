import { TestBed } from '@angular/core/testing';
import { AccountViewModel } from '@features/auth/presentation/pages/account/account.viewmodel';
import { LoadCurrentUserUseCase } from '@features/auth/domain/usecases/shared/load-current-user.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { GENERIC_ERROR_AR } from '@core/domain/errors/user-message';
import { CurrentUser } from '@features/auth/domain/model/shared/auth';

const user: CurrentUser = { userId: 'USR-1', email: 'a@b.c', fullName: 'أحمد', role: 'admin', phone: null, gender: null, age: null };

describe('AccountViewModel', () => {
  let loadCurrentUser: { run: jest.Mock };
  let notify: { error: jest.Mock; success: jest.Mock };

  function make(): AccountViewModel {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        AccountViewModel,
        { provide: LoadCurrentUserUseCase, useValue: loadCurrentUser },
        { provide: NotificationService, useValue: notify },
      ],
    });
    return TestBed.inject(AccountViewModel);
  }

  beforeEach(() => {
    loadCurrentUser = { run: jest.fn() };
    notify = { error: jest.fn(), success: jest.fn() };
  });

  it('loads the current user into the user signal', async () => {
    loadCurrentUser.run.mockResolvedValue(ok(user));
    const vm = make();
    await vm.init();
    expect(vm.user()).toEqual(user);
    expect(vm.loading()).toBe(false);
    expect(notify.error).not.toHaveBeenCalled();
  });

  it('notifies and leaves user null on failure', async () => {
    loadCurrentUser.run.mockResolvedValue(fail(new AppError('boom', 'network')));
    const vm = make();
    await vm.init();
    expect(vm.user()).toBeNull();
    expect(vm.loading()).toBe(false);
    expect(notify.error).toHaveBeenCalledWith(GENERIC_ERROR_AR);
  });
});
