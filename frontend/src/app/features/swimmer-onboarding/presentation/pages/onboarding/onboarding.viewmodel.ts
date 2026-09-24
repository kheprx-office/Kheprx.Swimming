import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { GetOnboardingPrefillUseCase } from '@features/swimmer-onboarding/domain/usecases/get-onboarding-prefill.use-case';
import { CompleteIdentityVitalsUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-identity-vitals.use-case';
import { LoadClubsUseCase, LoadGendersUseCase, LoadBloodTypesUseCase } from '@features/reference';
import { LoadFitnessAssessmentsUseCase } from '@features/reference/domain/usecases/load-fitness-assessments.use-case';
import { LookupItem } from '@features/reference/domain/model/reference';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

@Injectable()
export class OnboardingViewModel {
  private readonly getPrefillUc = inject(GetOnboardingPrefillUseCase);
  private readonly completeUc = inject(CompleteIdentityVitalsUseCase);
  private readonly loadClubs = inject(LoadClubsUseCase);
  private readonly loadGenders = inject(LoadGendersUseCase);
  private readonly loadBloodTypes = inject(LoadBloodTypesUseCase);
  private readonly loadFitness = inject(LoadFitnessAssessmentsUseCase);
  private readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly loading = signal(false);
  readonly error = signal(false);
  readonly submitting = signal(false);

  readonly clubs = signal<LookupItem[]>([]);
  readonly genders = signal<LookupItem[]>([]);
  readonly bloodTypes = signal<LookupItem[]>([]);
  readonly fitnessAssessments = signal<LookupItem[]>([]);

  // Identity (pre-filled, editable).
  readonly nameEn = signal('');
  readonly genderId = signal('');
  readonly dob = signal('');
  readonly trainingClubId = signal('');
  // Exam + vitals (new).
  readonly examDate = signal('');
  readonly bloodTypeId = signal('');
  readonly hemoglobin = signal('');
  readonly heightCm = signal('');
  readonly weightKg = signal('');
  readonly internalMedId = signal('');
  readonly heartAssessId = signal('');
  readonly spineAssessId = signal('');

  // name_ar is not shown (single "Full Name" field) — preserved from prefill and sent back untouched.
  private nameArOriginal: string | null = null;

  readonly canSubmit = computed(() =>
    this.nameEn().trim().length > 0 &&
    this.genderId().length > 0 &&
    this.dob().length > 0 &&
    this.trainingClubId().length > 0 &&
    this.examDate().length > 0 &&
    this.internalMedId().length > 0 &&
    this.heartAssessId().length > 0 &&
    this.spineAssessId().length > 0 &&
    Number(this.hemoglobin()) > 0 &&
    Number(this.heightCm()) > 0 &&
    Number(this.weightKg()) > 0);

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const [prefill, clubs, genders, blood, fitness] = await Promise.all([
      this.getPrefillUc.run(),
      this.loadClubs.run(),
      this.loadGenders.run(),
      this.loadBloodTypes.run(),
      this.loadFitness.run(),
    ]);
    if (clubs.ok) this.clubs.set(clubs.data);
    if (genders.ok) this.genders.set(genders.data);
    if (blood.ok) this.bloodTypes.set(blood.data);
    if (fitness.ok) this.fitnessAssessments.set(fitness.data);
    if (prefill.ok) {
      this.nameEn.set(prefill.data.nameEn);
      this.nameArOriginal = prefill.data.nameAr;
      this.genderId.set(prefill.data.genderId ?? '');
      this.dob.set(prefill.data.dob ?? '');
      this.trainingClubId.set(prefill.data.trainingClubId ?? '');
    } else {
      this.error.set(true);
    }
    this.loading.set(false);
  }

  async submit(): Promise<void> {
    if (!this.canSubmit() || this.submitting()) return;
    this.submitting.set(true);
    const r = await this.completeUc.run({
      nameEn: this.nameEn().trim(),
      nameAr: this.nameArOriginal,
      genderId: this.genderId(),
      dob: this.dob(),
      trainingClubId: this.trainingClubId(),
      examDate: this.examDate(),
      bloodTypeId: this.bloodTypeId() || null,
      hemoglobin: Number(this.hemoglobin()),
      heightCm: Number(this.heightCm()),
      weightKg: Number(this.weightKg()),
      internalMedId: this.internalMedId(),
      heartAssessId: this.heartAssessId(),
      spineAssessId: this.spineAssessId(),
    });
    this.submitting.set(false);
    if (r.ok) {
      this.auth.markOnboardingComplete();
      this.notify.success(this.i18n.t('onboarding.completed'));
      void this.router.navigate(['/home']);
    } else {
      this.notify.error(this.i18n.t('onboarding.saveFailed'));
    }
  }
}
