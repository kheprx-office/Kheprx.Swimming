import { Injectable, computed, inject, signal } from '@angular/core';
import { ListMedicalTestsUseCase } from '@features/medical-tests/domain/usecases/list-medical-tests.use-case';
import { CreateMedicalTestUseCase } from '@features/medical-tests/domain/usecases/create-medical-test.use-case';
import { DeleteMedicalTestUseCase } from '@features/medical-tests/domain/usecases/delete-medical-test.use-case';
import { MedicalTest } from '@features/medical-tests/domain/model/medical-test';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

@Injectable()
export class MedicalTestsViewModel {
  private readonly listTests = inject(ListMedicalTestsUseCase);
  private readonly createTest = inject(CreateMedicalTestUseCase);
  private readonly deleteTest = inject(DeleteMedicalTestUseCase);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly tests = signal<MedicalTest[]>([]);
  readonly loading = signal(false);
  readonly error = signal(false);

  readonly nameEn = signal('');
  readonly nameAr = signal('');
  readonly unit = signal('');
  readonly lowerBound = signal('');
  readonly upperBound = signal('');
  readonly submitting = signal(false);

  readonly canSubmit = computed(() => {
    const lo = Number(this.lowerBound());
    const hi = Number(this.upperBound());
    return this.nameEn().trim().length > 0
      && this.nameAr().trim().length > 0
      && this.unit().trim().length > 0
      && this.lowerBound().trim().length > 0
      && this.upperBound().trim().length > 0
      && Number.isFinite(lo) && Number.isFinite(hi)
      && hi > lo;
  });

  constructor() { void this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const r = await this.listTests.run();
    this.loading.set(false);
    if (r.ok) this.tests.set(r.data);
    else this.error.set(true);
  }

  async submit(): Promise<void> {
    if (!this.canSubmit() || this.submitting()) return;
    this.submitting.set(true);
    const r = await this.createTest.run({
      nameEn: this.nameEn().trim(),
      nameAr: this.nameAr().trim(),
      unit: this.unit().trim(),
      lowerBound: Number(this.lowerBound()),
      upperBound: Number(this.upperBound()),
    });
    this.submitting.set(false);
    if (r.ok) {
      this.tests.update((list) => [r.data, ...list]);
      this.resetForm();
      this.notify.success(this.i18n.t('medicalTests.toasts.created'));
    } else {
      this.notify.error(this.i18n.t('medicalTests.toasts.createFailed'));
    }
  }

  async remove(id: string): Promise<void> {
    const r = await this.deleteTest.run(id);
    if (r.ok) {
      this.tests.update((list) => list.filter((t) => t.id !== id));
      this.notify.success(this.i18n.t('medicalTests.toasts.deleted'));
    } else if (r.error.status === 404) {
      await this.load();
      this.notify.success(this.i18n.t('medicalTests.toasts.alreadyGone'));
    } else {
      this.notify.error(this.i18n.t('medicalTests.toasts.deleteFailed'));
    }
  }

  private resetForm(): void {
    for (const s of [this.nameEn, this.nameAr, this.unit, this.lowerBound, this.upperBound]) s.set('');
  }
}
