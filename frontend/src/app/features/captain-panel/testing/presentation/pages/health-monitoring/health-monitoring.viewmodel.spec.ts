import { TestBed } from '@angular/core/testing';
import { HealthMonitoringViewModel } from '@features/captain-panel/presentation/pages/health-monitoring/health-monitoring.viewmodel';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { ListMedicalTestsUseCase } from '@features/medical-tests/domain/usecases/list-medical-tests.use-case';
import { CreateHealthReadingUseCase } from '@features/health-readings/domain/usecases/create-health-reading.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

const swimmer = { id: 's1', uid: 'SW-1', nameEn: 'Ahmed', nameAr: null, clubNameEn: null, clubNameAr: null, gender: 'male', age: 15 };
const test1 = { id: 't1', nameEn: 'Glucose', nameAr: 'جلوكوز', unit: 'mg/dL', lowerBound: 70, upperBound: 110, createdAt: 'x' };

function build(over: { create?: unknown } = {}) {
  const swimmersUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [swimmer] }) };
  const testsUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [test1] }) };
  const createUc = { run: jest.fn().mockResolvedValue(over.create ?? { ok: true, data: { id: 'r1', status: 'normal' } }) };
  const notify = { success: jest.fn(), error: jest.fn() };
  const i18n = { t: (k: string) => k };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [
    HealthMonitoringViewModel,
    { provide: ListSwimmersUseCase, useValue: swimmersUc },
    { provide: ListMedicalTestsUseCase, useValue: testsUc },
    { provide: CreateHealthReadingUseCase, useValue: createUc },
    { provide: NotificationService, useValue: notify },
    { provide: TranslateService, useValue: i18n },
  ] });
  return { vm: TestBed.inject(HealthMonitoringViewModel), createUc, notify };
}

describe('HealthMonitoringViewModel', () => {
  it('loads swimmers and tests on construction', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    expect(vm.swimmerOptions()).toHaveLength(1);
    expect(vm.testOptions()).toHaveLength(1);
  });

  it('canSubmit requires swimmer, test and a numeric value', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    expect(vm.canSubmit()).toBe(false);
    vm.swimmerId.set('s1'); vm.testId.set('t1');
    expect(vm.canSubmit()).toBe(false);
    vm.value.set('95');
    expect(vm.canSubmit()).toBe(true);
  });

  it('selectedTest exposes the chosen test for the range helper', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    vm.testId.set('t1');
    expect(vm.selectedTest()?.upperBound).toBe(110);
  });

  it('submit logs the reading, toasts success and clears the value', async () => {
    const { vm, notify } = build();
    await Promise.resolve(); await Promise.resolve();
    vm.swimmerId.set('s1'); vm.testId.set('t1'); vm.value.set('95');
    await vm.submit();
    expect(notify.success).toHaveBeenCalledWith('healthMonitoring.toasts.loggedNormal');
    expect(vm.value()).toBe('');
  });

  it('submit shows an error toast on failure', async () => {
    const { vm, notify } = build({ create: { ok: false, error: { status: 500 } } });
    await Promise.resolve(); await Promise.resolve();
    vm.swimmerId.set('s1'); vm.testId.set('t1'); vm.value.set('95');
    await vm.submit();
    expect(notify.error).toHaveBeenCalledWith('healthMonitoring.toasts.createFailed');
  });
});
