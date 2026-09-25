import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { GetOnboardingPrefillUseCase } from '@features/swimmer-onboarding/domain/usecases/get-onboarding-prefill.use-case';
import { CompleteIdentityVitalsUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-identity-vitals.use-case';
import { CompleteGuardianMedicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-guardian-medical.use-case';
import { CompletePhysiologicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-physiological.use-case';
import { CompleteInBodyUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-inbody.use-case';
import { LoadClubsUseCase, LoadGendersUseCase, LoadBloodTypesUseCase } from '@features/reference';
import { LoadFitnessAssessmentsUseCase } from '@features/reference/domain/usecases/load-fitness-assessments.use-case';
import { LoadObservationCategoriesUseCase } from '@features/reference/domain/usecases/load-observation-categories.use-case';
import { LookupItem } from '@features/reference/domain/model/reference';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { AppError } from '@core/domain/errors/app-error';

@Injectable()
export class OnboardingViewModel {
  private readonly getPrefillUc = inject(GetOnboardingPrefillUseCase);
  private readonly completeUc = inject(CompleteIdentityVitalsUseCase);
  private readonly completeGuardianMedicalUc = inject(CompleteGuardianMedicalUseCase);
  private readonly completePhysiologicalUc = inject(CompletePhysiologicalUseCase);
  private readonly completeInBodyUc = inject(CompleteInBodyUseCase);
  private readonly loadClubs = inject(LoadClubsUseCase);
  private readonly loadGenders = inject(LoadGendersUseCase);
  private readonly loadBloodTypes = inject(LoadBloodTypesUseCase);
  private readonly loadFitness = inject(LoadFitnessAssessmentsUseCase);
  private readonly loadObservationCategories = inject(LoadObservationCategoriesUseCase);
  private readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly loading = signal(false);
  readonly error = signal(false);
  readonly submitting = signal(false);
  readonly stepError = signal<string | null>(null);

  readonly clubs = signal<LookupItem[]>([]);
  readonly genders = signal<LookupItem[]>([]);
  readonly bloodTypes = signal<LookupItem[]>([]);
  readonly fitnessAssessments = signal<LookupItem[]>([]);

  // Identity (pre-filled, editable).
  readonly nameEn = signal('');
  readonly nameAr = signal('');
  readonly phone = signal('');
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

  // Wizard step (1 = Identity & Vitals, 2 = Guardian & Medical, 3 = Physiological, 4 = InBody).
  readonly currentStep = signal<1 | 2 | 3 | 4>(1);

  readonly observationCategories = signal<LookupItem[]>([]);

  // Guardian (both parents required).
  readonly fatherName = signal(''); readonly fatherNationalId = signal(''); readonly fatherPhone = signal('');
  readonly motherName = signal(''); readonly motherNationalId = signal(''); readonly motherPhone = signal('');

  // Medical history — Yes/No + details per fixed category.
  readonly allergyYes = signal(false); readonly allergyDetails = signal('');
  readonly surgeryYes = signal(false); readonly surgeryDetails = signal('');
  readonly chronicYes = signal(false); readonly chronicDetails = signal('');
  readonly autoimmuneYes = signal(false); readonly autoimmuneDetails = signal('');

  // Physiological (Step 3) — seven body-measurement values, stored 1:1 (matches the profile Body Measurements tab).
  readonly rightArmCm = signal(''); readonly leftArmCm = signal(''); readonly rightLegCm = signal(''); readonly leftLegCm = signal('');
  readonly torsoCm = signal(''); readonly bustDiameterCm = signal(''); readonly waistDiameterCm = signal('');

  // InBody (Step 4) — seven measurements (ib-prefixed to avoid colliding with Step-1 vitals height/weight).
  readonly ibHeightCm = signal(''); readonly ibWeightKg = signal(''); readonly ibFatPct = signal(''); readonly ibMusclePct = signal('');
  readonly ibWaterPct = signal(''); readonly ibBoneDensity = signal(''); readonly ibBodyDensity = signal('');

  // Fixed medical categories: code (matches reference.observation_category) + stored English field label.
  private readonly medicalDefs = [
    { code: 'allergy', label: 'Allergies', yes: this.allergyYes, details: this.allergyDetails },
    { code: 'surgery', label: 'Previous Surgeries', yes: this.surgeryYes, details: this.surgeryDetails },
    { code: 'chronic', label: 'Chronic Diseases', yes: this.chronicYes, details: this.chronicDetails },
    { code: 'autoimmune', label: 'Autoimmune Diseases', yes: this.autoimmuneYes, details: this.autoimmuneDetails },
  ];

  readonly canSubmitStep1 = computed(() =>
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

  readonly canSubmitStep2 = computed(() =>
    this.fatherName().trim().length > 0 && this.fatherNationalId().trim().length > 0 && this.fatherPhone().trim().length > 0 &&
    this.motherName().trim().length > 0 && this.motherNationalId().trim().length > 0 && this.motherPhone().trim().length > 0 &&
    this.medicalDefs.every((d) => !d.yes() || d.details().trim().length > 0));

  readonly canSubmitStep3 = computed(() =>
    [this.rightArmCm(), this.leftArmCm(), this.rightLegCm(), this.leftLegCm(), this.torsoCm(), this.bustDiameterCm(), this.waistDiameterCm()]
      .every((s) => { const n = Number(s); return n > 0 && n <= 999.9; }));

  readonly canSubmitStep4 = computed(() => {
    const ok = (s: string, lo: number, hi: number, inclusiveLo: boolean) => {
      if (s.trim().length === 0) return false;
      const n = Number(s);
      return (inclusiveLo ? n >= lo : n > lo) && n <= hi;
    };
    return ok(this.ibHeightCm(), 0, 999.9, false) && ok(this.ibWeightKg(), 0, 999.9, false) &&
      ok(this.ibFatPct(), 0, 100, true) && ok(this.ibMusclePct(), 0, 100, true) && ok(this.ibWaterPct(), 0, 100, true) &&
      ok(this.ibBoneDensity(), 0, 99.99, false) && ok(this.ibBodyDensity(), 0, 99.99, false);
  });

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const [prefill, clubs, genders, blood, fitness, cats] = await Promise.all([
      this.getPrefillUc.run(),
      this.loadClubs.run(),
      this.loadGenders.run(),
      this.loadBloodTypes.run(),
      this.loadFitness.run(),
      this.loadObservationCategories.run(),
    ]);
    if (clubs.ok) this.clubs.set(clubs.data);
    if (genders.ok) this.genders.set(genders.data);
    if (blood.ok) this.bloodTypes.set(blood.data);
    if (fitness.ok) this.fitnessAssessments.set(fitness.data);
    if (cats.ok) this.observationCategories.set(cats.data);
    if (prefill.ok) {
      this.nameEn.set(prefill.data.nameEn);
      this.nameAr.set(prefill.data.nameAr ?? '');
      this.phone.set(prefill.data.phone ?? '');
      this.genderId.set(prefill.data.genderId ?? '');
      this.dob.set(prefill.data.dob ?? '');
      this.trainingClubId.set(prefill.data.trainingClubId ?? '');
    } else {
      this.error.set(true);
    }
    this.loading.set(false);
  }

  backToStep1(): void { this.stepError.set(null); this.currentStep.set(1); }
  backToStep2(): void { this.stepError.set(null); this.currentStep.set(2); }
  backToStep3(): void { this.stepError.set(null); this.currentStep.set(3); }

  private describeError(err: AppError): string {
    // The backend packs the specific field message(s) into the error body (AppError.code) —
    // surface it verbatim on a 400 instead of a generic toast (server is the validation gate).
    if (err.status === 400 && err.code) return err.code;
    return this.i18n.t('onboarding.saveFailed');
  }

  private failStep(err: AppError): void {
    const message = this.describeError(err);
    this.stepError.set(message);
    this.notify.error(message);
  }

  async saveStep1(): Promise<void> {
    if (!this.canSubmitStep1() || this.submitting()) return;
    this.submitting.set(true);
    this.stepError.set(null);
    const r = await this.completeUc.run({
      nameEn: this.nameEn().trim(), nameAr: this.nameAr().trim() || null,
      genderId: this.genderId(), dob: this.dob(), trainingClubId: this.trainingClubId(),
      examDate: this.examDate(), bloodTypeId: this.bloodTypeId() || null,
      hemoglobin: Number(this.hemoglobin()), heightCm: Number(this.heightCm()), weightKg: Number(this.weightKg()),
      internalMedId: this.internalMedId(), heartAssessId: this.heartAssessId(), spineAssessId: this.spineAssessId(),
      phone: this.phone().trim() || null,
    });
    this.submitting.set(false);
    if (r.ok) this.currentStep.set(2); else this.failStep(r.error);
  }

  async saveStep2(): Promise<void> {
    if (!this.canSubmitStep2() || this.submitting()) return;
    this.stepError.set(null);

    // Guard against unresolved category ids — check before persisting anything.
    const medicalItems = this.buildMedical();
    if (medicalItems.some((item) => item.categoryId === '')) {
      const message = this.i18n.t('onboarding.saveFailed');
      this.stepError.set(message);
      this.notify.error(message);
      return;
    }

    this.submitting.set(true);
    const r = await this.completeGuardianMedicalUc.run({
      father: { name: this.fatherName().trim(), nationalId: this.fatherNationalId().trim(), phone: this.fatherPhone().trim() },
      mother: { name: this.motherName().trim(), nationalId: this.motherNationalId().trim(), phone: this.motherPhone().trim() },
      medical: medicalItems,
    });
    this.submitting.set(false);
    if (r.ok) this.currentStep.set(3); else this.failStep(r.error);
  }

  async saveStep3(): Promise<void> {
    if (!this.canSubmitStep3() || this.submitting()) return;
    this.submitting.set(true);
    this.stepError.set(null);
    const r = await this.completePhysiologicalUc.run({
      rightArmCm: Number(this.rightArmCm()), leftArmCm: Number(this.leftArmCm()),
      rightLegCm: Number(this.rightLegCm()), leftLegCm: Number(this.leftLegCm()),
      torsoCm: Number(this.torsoCm()), bustDiameterCm: Number(this.bustDiameterCm()), waistDiameterCm: Number(this.waistDiameterCm()),
    });
    this.submitting.set(false);
    if (r.ok) this.currentStep.set(4); else this.failStep(r.error);
  }

  async submit(): Promise<void> {
    if (!this.canSubmitStep4() || this.submitting()) return;
    this.submitting.set(true);
    this.stepError.set(null);

    // Saves the reading AND completes onboarding (server-side, at the InBody step).
    const r = await this.completeInBodyUc.run({
      heightCm: Number(this.ibHeightCm()), weightKg: Number(this.ibWeightKg()),
      fatPct: Number(this.ibFatPct()), musclePct: Number(this.ibMusclePct()), waterPct: Number(this.ibWaterPct()),
      boneDensity: Number(this.ibBoneDensity()), bodyDensity: Number(this.ibBodyDensity()),
    });
    this.submitting.set(false);

    if (r.ok) {
      this.auth.markOnboardingComplete();
      this.notify.success(this.i18n.t('onboarding.completed'));
      void this.router.navigate(['/my-profile']);
    } else {
      this.failStep(r.error);
    }
  }

  private buildMedical() {
    const cats = this.observationCategories();
    const idOf = (code: string) => cats.find((c) => c.code === code)?.id ?? '';
    return this.medicalDefs
      .filter((d) => d.yes() && d.details().trim().length > 0)
      .map((d) => ({ categoryId: idOf(d.code), fieldLabel: d.label, value: d.details().trim() }));
  }
}
