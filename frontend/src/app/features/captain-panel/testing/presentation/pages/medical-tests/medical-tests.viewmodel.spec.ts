import { TestBed } from '@angular/core/testing';
import { MedicalTestsViewModel } from '@features/captain-panel/presentation/pages/medical-tests/medical-tests.viewmodel';
import { ListMedicalTestsUseCase } from '@features/medical-tests/domain/usecases/list-medical-tests.use-case';
import { CreateMedicalTestUseCase } from '@features/medical-tests/domain/usecases/create-medical-test.use-case';
import { DeleteMedicalTestUseCase } from '@features/medical-tests/domain/usecases/delete-medical-test.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

const test1 = { id: '1', nameEn: 'Hemoglobin', nameAr: 'هيموغلوبين', unit: 'g/dL', lowerBound: 11, upperBound: 17.5, createdAt: 'x' };

function build(over: {
  list?: unknown; create?: unknown; del?: unknown;
} = {}) {
  const listUc = { run: jest.fn().mockResolvedValue(over.list ?? { ok: true, data: [test1] }) };
  const createUc = { run: jest.fn().mockResolvedValue(over.create ?? { ok: true, data: { ...test1, id: '2', nameEn: 'Glucose' } }) };
  const deleteUc = { run: jest.fn().mockResolvedValue(over.del ?? { ok: true, data: undefined }) };
  const notify = { success: jest.fn(), error: jest.fn() };
  const i18n = { t: (k: string) => k };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [
    MedicalTestsViewModel,
    { provide: ListMedicalTestsUseCase, useValue: listUc },
    { provide: CreateMedicalTestUseCase, useValue: createUc },
    { provide: DeleteMedicalTestUseCase, useValue: deleteUc },
    { provide: NotificationService, useValue: notify },
    { provide: TranslateService, useValue: i18n },
  ] });
  return { vm: TestBed.inject(MedicalTestsViewModel), listUc, createUc, deleteUc, notify };
}

describe('MedicalTestsViewModel', () => {
  it('loads tests on construction', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    expect(vm.tests()).toHaveLength(1);
    expect(vm.tests()[0].nameEn).toBe('Hemoglobin');
  });

  it('canSubmit requires all fields and upper > lower', async () => {
    const { vm } = build();
    expect(vm.canSubmit()).toBe(false);
    vm.nameEn.set('Glucose'); vm.nameAr.set('جلوكوز'); vm.unit.set('mg/dL');
    vm.lowerBound.set('70'); vm.upperBound.set('60'); // upper < lower
    expect(vm.canSubmit()).toBe(false);
    vm.upperBound.set('110');
    expect(vm.canSubmit()).toBe(true);
  });

  it('submit prepends the created test and resets the form', async () => {
    const { vm, notify } = build();
    await Promise.resolve(); await Promise.resolve();
    vm.nameEn.set('Glucose'); vm.nameAr.set('جلوكوز'); vm.unit.set('mg/dL');
    vm.lowerBound.set('70'); vm.upperBound.set('110');
    await vm.submit();
    expect(vm.tests()[0].nameEn).toBe('Glucose'); // prepended
    expect(vm.nameEn()).toBe('');                  // reset
    expect(notify.success).toHaveBeenCalled();
  });

  it('remove drops the row on success', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    await vm.remove('1');
    expect(vm.tests()).toHaveLength(0);
  });

  it('remove on 404 reloads the list and shows a non-error toast', async () => {
    const { vm, notify } = build({ del: { ok: false, error: { status: 404 } } });
    await Promise.resolve(); await Promise.resolve();
    await vm.remove('1');
    expect(vm.tests()).toHaveLength(1);      // reloaded from the default list mock
    expect(notify.success).toHaveBeenCalled();
  });

  it('remove on a non-404 error keeps the row and shows an error toast', async () => {
    const { vm, notify } = build({ del: { ok: false, error: { status: 500 } } });
    await Promise.resolve(); await Promise.resolve();
    await vm.remove('1');
    expect(vm.tests()).toHaveLength(1);      // unchanged
    expect(notify.error).toHaveBeenCalled();
  });
});
