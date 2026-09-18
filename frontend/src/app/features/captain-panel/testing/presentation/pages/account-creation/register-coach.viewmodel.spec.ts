import { TestBed } from '@angular/core/testing';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { RegisterCoachViewModel } from '@features/captain-panel/presentation/pages/account-creation/register-coach.viewmodel';
import { LoadGendersUseCase } from '@features/reference';
import { CreateCoachUseCase } from '@features/coaches/domain/usecases/create-coach.use-case';
import { NotificationService } from '@core/ui/notification.service';

const genders = [{ id: 'g1', code: 'male', nameEn: 'Male', nameAr: 'ذكر' }];

function build(create = { run: jest.fn() }) {
  const notify = { success: jest.fn(), error: jest.fn() } as unknown as NotificationService;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      RegisterCoachViewModel,
      { provide: LoadGendersUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(genders)) } },
      { provide: CreateCoachUseCase, useValue: create },
      { provide: NotificationService, useValue: notify },
    ],
  });
  return { vm: TestBed.inject(RegisterCoachViewModel), notify };
}

function fill(vm: RegisterCoachViewModel) {
  vm.nameEn.set('Dave'); vm.username.set('dave'); vm.email.set('d@o.com');
  vm.nationalId.set('29001011234567'); vm.genderId.set('g1'); vm.dob.set('1990-01-01'); vm.phone.set('01000000000');
}

describe('RegisterCoachViewModel', () => {
  it('loads genders', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    expect(vm.genders().length).toBe(1);
  });

  it('blocks submit until required fields (incl. 14-digit national id) are set', () => {
    const { vm } = build();
    expect(vm.canSubmit()).toBe(false);
    fill(vm);
    expect(vm.canSubmit()).toBe(true);
    vm.nationalId.set('123'); // invalid
    expect(vm.canSubmit()).toBe(false);
  });

  it('creates and exposes credentials on success', async () => {
    const created = { id: 'i', username: 'dave', nameEn: 'Dave', role: 'captain', temporaryPassword: 'Oasis2026!' };
    const create = { run: jest.fn().mockResolvedValue(ok(created)) };
    const { vm, notify } = build(create);
    fill(vm);
    await vm.submit();
    expect(create.run).toHaveBeenCalled();
    expect(vm.created()?.temporaryPassword).toBe('Oasis2026!');
    expect(notify.success).toHaveBeenCalled();
  });

  it('surfaces a conflict', async () => {
    const create = { run: jest.fn().mockResolvedValue(fail(new AppError('taken', 'http', 409))) };
    const { vm, notify } = build(create);
    fill(vm);
    await vm.submit();
    expect(vm.created()).toBeNull();
    expect(notify.error).toHaveBeenCalled();
  });
});
