import { TestBed } from '@angular/core/testing';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { RegisterSwimmerViewModel } from '@features/captain-panel/presentation/pages/account-creation/register-swimmer.viewmodel';
import { LoadClubsUseCase, LoadGendersUseCase, LoadStrokesUseCase } from '@features/reference';
import { CreateSwimmerUseCase } from '@features/swimmers/domain/usecases/create-swimmer.use-case';
import { NotificationService } from '@core/ui/notification.service';

const item = [{ id: 'x1', nameEn: 'X', nameAr: null }];
const lookups = () => ({ run: jest.fn().mockResolvedValue(ok(item)) });

function build(create = { run: jest.fn() }) {
  const notify = { success: jest.fn(), error: jest.fn() } as unknown as NotificationService;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      RegisterSwimmerViewModel,
      { provide: LoadClubsUseCase, useValue: lookups() },
      { provide: LoadGendersUseCase, useValue: lookups() },
      { provide: LoadStrokesUseCase, useValue: lookups() },
      { provide: CreateSwimmerUseCase, useValue: create },
      { provide: NotificationService, useValue: notify },
    ],
  });
  return { vm: TestBed.inject(RegisterSwimmerViewModel), notify };
}

describe('RegisterSwimmerViewModel', () => {
  it('loads lookups into signals', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    expect(vm.clubs().length).toBe(1);
    expect(vm.strokes().length).toBe(1);
  });

  it('blocks submit until required fields are set', () => {
    const { vm } = build();
    expect(vm.canSubmit()).toBe(false);
  });

  it('creates and exposes the returned credentials on success', async () => {
    const created = { id: 'i', uid: 'SW-0007', username: 'mona', nameEn: 'Mona', temporaryPassword: 'Oasis2026!' };
    const create = { run: jest.fn().mockResolvedValue(ok(created)) };
    const { vm, notify } = build(create);
    vm.nameEn.set('Mona'); vm.username.set('mona'); vm.trainingClubId.set('c1');
    vm.genderId.set('g1'); vm.dob.set('2010-05-01'); vm.strokeIds.set(['s1']);
    await vm.submit();
    expect(create.run).toHaveBeenCalled();
    expect(vm.created()?.uid).toBe('SW-0007');
    expect(notify.success).toHaveBeenCalled();
  });

  it('surfaces a conflict error', async () => {
    const create = { run: jest.fn().mockResolvedValue(fail(new AppError('taken', 'http', 409))) };
    const { vm, notify } = build(create);
    vm.nameEn.set('Mona'); vm.username.set('mona'); vm.trainingClubId.set('c1');
    vm.genderId.set('g1'); vm.dob.set('2010-05-01'); vm.strokeIds.set(['s1']);
    await vm.submit();
    expect(vm.created()).toBeNull();
    expect(notify.error).toHaveBeenCalled();
  });
});
