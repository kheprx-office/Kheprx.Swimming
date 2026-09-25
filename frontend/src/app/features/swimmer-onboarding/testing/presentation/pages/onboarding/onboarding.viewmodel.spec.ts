import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { OnboardingViewModel } from '@features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel';
import { GetOnboardingPrefillUseCase } from '@features/swimmer-onboarding/domain/usecases/get-onboarding-prefill.use-case';
import { CompleteIdentityVitalsUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-identity-vitals.use-case';
import { CompleteGuardianMedicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-guardian-medical.use-case';
import { CompletePhysiologicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-physiological.use-case';
import { CompleteInBodyUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-inbody.use-case';
import { LoadClubsUseCase, LoadGendersUseCase, LoadBloodTypesUseCase } from '@features/reference';
import { LoadFitnessAssessmentsUseCase } from '@features/reference/domain/usecases/load-fitness-assessments.use-case';
import { LoadObservationCategoriesUseCase } from '@features/reference/domain/usecases/load-observation-categories.use-case';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';

const PREFILL = { uid: 'SW-1', nameEn: 'Sam', nameAr: 'سام', genderId: 'g1', dob: '2010-01-01', trainingClubId: 'c1', phone: '01099999999' };
const LOOKUP = [{ id: 'x', nameEn: 'X', nameAr: null }];
const CATS = [
  { id: 'cat-allergy', code: 'allergy', nameEn: 'Allergy', nameAr: null },
  { id: 'cat-surgery', code: 'surgery', nameEn: 'Surgery', nameAr: null },
  { id: 'cat-chronic', code: 'chronic', nameEn: 'Chronic', nameAr: null },
  { id: 'cat-autoimmune', code: 'autoimmune', nameEn: 'Autoimmune', nameAr: null },
];

function build(overrides: { identity?: unknown; guardianMedical?: unknown; physiological?: unknown; inbody?: unknown; cats?: unknown } = {}) {
  const getPrefill = { run: jest.fn().mockResolvedValue(ok(PREFILL)) };
  const identity = { run: jest.fn().mockResolvedValue(overrides.identity ?? ok(undefined)) };
  const guardianMedical = { run: jest.fn().mockResolvedValue(overrides.guardianMedical ?? ok(undefined)) };
  const physiological = { run: jest.fn().mockResolvedValue(overrides.physiological ?? ok(undefined)) };
  const inbody = { run: jest.fn().mockResolvedValue(overrides.inbody ?? ok(undefined)) };
  const lookups = { run: jest.fn().mockResolvedValue(ok(LOOKUP)) };
  const cats = { run: jest.fn().mockResolvedValue(overrides.cats ?? ok(CATS)) };
  const auth = { markOnboardingComplete: jest.fn() } as unknown as AuthSessionStore;
  const router = { navigate: jest.fn() } as unknown as Router;
  const notify = { success: jest.fn(), error: jest.fn() } as unknown as NotificationService;
  const i18n = { t: (k: string) => k } as unknown as TranslateService;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      OnboardingViewModel,
      { provide: GetOnboardingPrefillUseCase, useValue: getPrefill },
      { provide: CompleteIdentityVitalsUseCase, useValue: identity },
      { provide: CompleteGuardianMedicalUseCase, useValue: guardianMedical },
      { provide: CompletePhysiologicalUseCase, useValue: physiological },
      { provide: CompleteInBodyUseCase, useValue: inbody },
      { provide: LoadClubsUseCase, useValue: lookups },
      { provide: LoadGendersUseCase, useValue: lookups },
      { provide: LoadBloodTypesUseCase, useValue: lookups },
      { provide: LoadFitnessAssessmentsUseCase, useValue: lookups },
      { provide: LoadObservationCategoriesUseCase, useValue: cats },
      { provide: AuthSessionStore, useValue: auth },
      { provide: Router, useValue: router },
      { provide: NotificationService, useValue: notify },
      { provide: TranslateService, useValue: i18n },
    ],
  });
  return { vm: TestBed.inject(OnboardingViewModel), identity, guardianMedical, physiological, inbody, auth, router, notify };
}

function fillStep1(vm: OnboardingViewModel) {
  vm.examDate.set('2026-01-01'); vm.internalMedId.set('f1'); vm.heartAssessId.set('f1'); vm.spineAssessId.set('f1');
  vm.hemoglobin.set('14.5'); vm.heightCm.set('175'); vm.weightKg.set('68');
}
function fillStep2(vm: OnboardingViewModel) {
  vm.fatherName.set('Ahmed'); vm.fatherNationalId.set('12345678901234'); vm.fatherPhone.set('010');
  vm.motherName.set('Sara'); vm.motherNationalId.set('43210987654321'); vm.motherPhone.set('011');
}
function fillStep3(vm: OnboardingViewModel) {
  vm.rightArmCm.set('32.5'); vm.leftArmCm.set('31'); vm.rightLegCm.set('95'); vm.leftLegCm.set('94');
  vm.torsoCm.set('60'); vm.bustDiameterCm.set('90'); vm.waistDiameterCm.set('75');
}
function fillStep4(vm: OnboardingViewModel) {
  vm.ibHeightCm.set('175'); vm.ibWeightKg.set('68'); vm.ibFatPct.set('15'); vm.ibMusclePct.set('40');
  vm.ibWaterPct.set('55'); vm.ibBoneDensity.set('3.2'); vm.ibBodyDensity.set('1.05');
}

describe('OnboardingViewModel', () => {
  it('load() seeds identity drafts, lookups, and observation categories', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.nameEn()).toBe('Sam');
    expect(vm.nameAr()).toBe('سام');
    expect(vm.phone()).toBe('01099999999');
    expect(vm.genderId()).toBe('g1');
    expect(vm.trainingClubId()).toBe('c1');
    expect(vm.observationCategories().length).toBe(4);
  });

  it('canSubmitStep4 requires all seven non-empty and in-bounds (0 allowed for %, blank rejected)', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.canSubmitStep4()).toBe(false);
    fillStep4(vm);
    expect(vm.canSubmitStep4()).toBe(true);
    vm.ibWaterPct.set('');
    expect(vm.canSubmitStep4()).toBe(false);
    vm.ibWaterPct.set('0');
    expect(vm.canSubmitStep4()).toBe(true);
    vm.ibFatPct.set('101');
    expect(vm.canSubmitStep4()).toBe(false);
  });

  it('saveStep1() posts identity-vitals and advances to step 2 on success', async () => {
    const { vm, identity } = build();
    await vm.load();
    fillStep1(vm);
    await vm.saveStep1();
    expect(identity.run).toHaveBeenCalledTimes(1);
    expect(identity.run).toHaveBeenCalledWith(expect.objectContaining({ nameEn: 'Sam', nameAr: 'سام', phone: '01099999999' }));
    expect(vm.currentStep()).toBe(2);
    expect(vm.stepError()).toBeNull();
  });

  it('saveStep1() stays on step 1 and surfaces the specific 400 message on failure', async () => {
    const { vm, notify } = build({ identity: fail(new AppError('Date of birth must be in the past', 'http', 400, 'Date of birth must be in the past')) });
    await vm.load();
    fillStep1(vm);
    await vm.saveStep1();
    expect(vm.currentStep()).toBe(1);
    expect(vm.stepError()).toBe('Date of birth must be in the past');
    expect(notify.error).toHaveBeenCalledWith('Date of birth must be in the past');
  });

  it('saveStep1() shows the generic message on a non-400 failure', async () => {
    const { vm, notify } = build({ identity: fail(new AppError('nope', 'network')) });
    await vm.load();
    fillStep1(vm);
    await vm.saveStep1();
    expect(vm.currentStep()).toBe(1);
    expect(vm.stepError()).toBe('onboarding.saveFailed');
    expect(notify.error).toHaveBeenCalledWith('onboarding.saveFailed');
  });

  it('re-clicking saveStep1 re-POSTs (no latch)', async () => {
    const { vm, identity } = build();
    await vm.load();
    fillStep1(vm);
    await vm.saveStep1();
    vm.backToStep1();
    await vm.saveStep1();
    expect(identity.run).toHaveBeenCalledTimes(2);
  });

  it('saveStep2() posts guardian-medical and advances to step 3 on success', async () => {
    const { vm, guardianMedical } = build();
    await vm.load();
    fillStep1(vm); await vm.saveStep1();
    fillStep2(vm); await vm.saveStep2();
    expect(guardianMedical.run).toHaveBeenCalledTimes(1);
    expect(vm.currentStep()).toBe(3);
  });

  it('saveStep2() blocks (posts nothing) when a medical "Yes" has an unresolved category id', async () => {
    const { vm, guardianMedical, notify } = build({ cats: ok([]) });
    await vm.load();
    fillStep1(vm); await vm.saveStep1();
    fillStep2(vm);
    vm.allergyYes.set(true); vm.allergyDetails.set('Peanuts');
    await vm.saveStep2();
    expect(guardianMedical.run).not.toHaveBeenCalled();
    expect(vm.currentStep()).toBe(2);
    expect(vm.stepError()).not.toBeNull();
    expect(notify.error).toHaveBeenCalled();
  });

  it('saveStep3() posts physiological and advances to step 4 on success', async () => {
    const { vm, physiological } = build();
    await vm.load();
    fillStep1(vm); await vm.saveStep1();
    fillStep2(vm); await vm.saveStep2();
    fillStep3(vm); await vm.saveStep3();
    expect(physiological.run).toHaveBeenCalledWith({ rightArmCm: 32.5, leftArmCm: 31, rightLegCm: 95, leftLegCm: 94, torsoCm: 60, bustDiameterCm: 90, waistDiameterCm: 75 });
    expect(vm.currentStep()).toBe(4);
  });

  it('saveStep3() stays on step 3 and surfaces the error on failure', async () => {
    const { vm, notify } = build({ physiological: fail(new AppError('nope', 'network')) });
    await vm.load();
    fillStep1(vm); await vm.saveStep1();
    fillStep2(vm); await vm.saveStep2();
    fillStep3(vm); await vm.saveStep3();
    expect(vm.currentStep()).toBe(3);
    expect(vm.stepError()).not.toBeNull();
    expect(notify.error).toHaveBeenCalled();
  });

  it('submit() posts inbody, completes, and navigates on success', async () => {
    const { vm, inbody, auth, router } = build();
    await vm.load();
    fillStep1(vm); await vm.saveStep1();
    fillStep2(vm); await vm.saveStep2();
    fillStep3(vm); await vm.saveStep3();
    fillStep4(vm);
    await vm.submit();
    expect(inbody.run).toHaveBeenCalledWith({ heightCm: 175, weightKg: 68, fatPct: 15, musclePct: 40, waterPct: 55, boneDensity: 3.2, bodyDensity: 1.05 });
    expect(auth.markOnboardingComplete).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/my-profile']);
  });

  it('submit() stays on step 4 and does not complete when inbody fails', async () => {
    const { vm, auth, router, notify } = build({ inbody: fail(new AppError('bad hemoglobin', 'http', 400, 'bad hemoglobin')) });
    await vm.load();
    fillStep1(vm); await vm.saveStep1();
    fillStep2(vm); await vm.saveStep2();
    fillStep3(vm); await vm.saveStep3();
    fillStep4(vm);
    await vm.submit();
    expect(vm.currentStep()).toBe(4);
    expect(auth.markOnboardingComplete).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    expect(notify.error).toHaveBeenCalledWith('bad hemoglobin');
  });

  it('backToStep1() navigates without posting and clears the step error', async () => {
    const { vm, identity } = build({ identity: fail(new AppError('x', 'network')) });
    await vm.load();
    fillStep1(vm); await vm.saveStep1();       // fails → stepError set, stays on 1
    expect(vm.stepError()).not.toBeNull();
    vm.currentStep.set(2);
    vm.backToStep1();
    expect(vm.currentStep()).toBe(1);
    expect(vm.stepError()).toBeNull();
    expect(identity.run).toHaveBeenCalledTimes(1); // Back did not re-post
  });
});
