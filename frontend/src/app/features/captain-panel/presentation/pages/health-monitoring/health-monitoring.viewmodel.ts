import { Injectable, computed, inject, signal } from '@angular/core';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { ListMedicalTestsUseCase } from '@features/medical-tests/domain/usecases/list-medical-tests.use-case';
import { CreateHealthReadingUseCase } from '@features/health-readings/domain/usecases/create-health-reading.use-case';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import { MedicalTest } from '@features/medical-tests/domain/model/medical-test';
import { LookupItem } from '@features/reference/domain/model/reference';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

@Injectable()
export class HealthMonitoringViewModel {
  private readonly listSwimmers = inject(ListSwimmersUseCase);
  private readonly listTests = inject(ListMedicalTestsUseCase);
  private readonly createReading = inject(CreateHealthReadingUseCase);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly swimmers = signal<SwimmerListItem[]>([]);
  readonly tests = signal<MedicalTest[]>([]);
  readonly loading = signal(false);
  readonly error = signal(false);

  readonly swimmerId = signal('');
  readonly testId = signal('');
  readonly value = signal('');
  readonly submitting = signal(false);

  readonly swimmerOptions = computed<LookupItem[]>(() =>
    this.swimmers().map((s) => ({ id: s.id, nameEn: s.nameEn, nameAr: s.nameAr })));

  readonly testOptions = computed<LookupItem[]>(() =>
    this.tests().map((t) => ({ id: t.id, nameEn: t.nameEn, nameAr: t.nameAr })));

  readonly selectedTest = computed<MedicalTest | null>(() =>
    this.tests().find((t) => t.id === this.testId()) ?? null);

  readonly canSubmit = computed(() => {
    const v = Number(this.value());
    return this.swimmerId().length > 0
      && this.testId().length > 0
      && this.value().trim().length > 0
      && Number.isFinite(v);
  });

  constructor() { void this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const [s, t] = await Promise.all([this.listSwimmers.run(undefined), this.listTests.run()]);
    this.loading.set(false);
    if (s.ok) this.swimmers.set(s.data); else this.error.set(true);
    if (t.ok) this.tests.set(t.data); else this.error.set(true);
  }

  async submit(): Promise<void> {
    if (!this.canSubmit() || this.submitting()) return;
    this.submitting.set(true);
    const r = await this.createReading.run({
      swimmerId: this.swimmerId(),
      medicalTestId: this.testId(),
      value: Number(this.value()),
    });
    this.submitting.set(false);
    if (r.ok) {
      const key = r.data.status === 'normal'
        ? 'healthMonitoring.toasts.loggedNormal'
        : 'healthMonitoring.toasts.loggedOut';
      this.notify.success(this.i18n.t(key));
      this.value.set('');
    } else {
      this.notify.error(this.i18n.t('healthMonitoring.toasts.createFailed'));
    }
  }
}
