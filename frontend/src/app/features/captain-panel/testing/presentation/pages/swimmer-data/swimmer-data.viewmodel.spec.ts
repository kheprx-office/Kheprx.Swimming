import { TestBed } from '@angular/core/testing';
import { SwimmerDataViewModel } from '@features/captain-panel/presentation/pages/swimmer-data/swimmer-data.viewmodel';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { LoadObservationCategoriesUseCase } from '@features/reference/domain/usecases/load-observation-categories.use-case';
import { CreateObservationUseCase } from '@features/observations/domain/usecases/create-observation.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

const swimmer = { id: 's1', uid: 'SW-1', nameEn: 'Ahmed', nameAr: null, clubNameEn: null, clubNameAr: null, gender: 'male', age: 15 };
const category = { id: 'c1', code: 'allergy', nameEn: 'Allergy', nameAr: 'حساسية' };

function build(over: { create?: unknown } = {}) {
  const swimmersUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [swimmer] }) };
  const catsUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [category] }) };
  const createUc = { run: jest.fn().mockResolvedValue(over.create ?? { ok: true, data: { id: 'o1' } }) };
  const notify = { success: jest.fn(), error: jest.fn() };
  const i18n = { t: (k: string) => k };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [
    SwimmerDataViewModel,
    { provide: ListSwimmersUseCase, useValue: swimmersUc },
    { provide: LoadObservationCategoriesUseCase, useValue: catsUc },
    { provide: CreateObservationUseCase, useValue: createUc },
    { provide: NotificationService, useValue: notify },
    { provide: TranslateService, useValue: i18n },
  ] });
  return { vm: TestBed.inject(SwimmerDataViewModel), createUc, notify };
}

describe('SwimmerDataViewModel', () => {
  it('loads swimmers and categories on construction', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    expect(vm.swimmerOptions()).toHaveLength(1);
    expect(vm.categoryOptions()).toHaveLength(1);
  });

  it('canSubmit requires swimmer, category, field name and value', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    expect(vm.canSubmit()).toBe(false);
    vm.swimmerId.set('s1'); vm.categoryId.set('c1'); vm.fieldLabel.set('Penicillin');
    expect(vm.canSubmit()).toBe(false);
    vm.value.set('Severe');
    expect(vm.canSubmit()).toBe(true);
  });

  it('submit adds the data, toasts success and clears field + value (keeps swimmer + category)', async () => {
    const { vm, notify } = build();
    await Promise.resolve(); await Promise.resolve();
    vm.swimmerId.set('s1'); vm.categoryId.set('c1'); vm.fieldLabel.set('Penicillin'); vm.value.set('Severe');
    await vm.submit();
    expect(notify.success).toHaveBeenCalledWith('swimmerData.toasts.added');
    expect(vm.fieldLabel()).toBe('');
    expect(vm.value()).toBe('');
    expect(vm.swimmerId()).toBe('s1');
    expect(vm.categoryId()).toBe('c1');
  });

  it('submit shows an error toast on failure', async () => {
    const { vm, notify } = build({ create: { ok: false, error: { status: 500 } } });
    await Promise.resolve(); await Promise.resolve();
    vm.swimmerId.set('s1'); vm.categoryId.set('c1'); vm.fieldLabel.set('Penicillin'); vm.value.set('Severe');
    await vm.submit();
    expect(notify.error).toHaveBeenCalledWith('swimmerData.toasts.addFailed');
  });
});
