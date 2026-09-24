import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { OnboardingViewModel } from '@features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel';
import { GetOnboardingPrefillUseCase } from '@features/swimmer-onboarding/domain/usecases/get-onboarding-prefill.use-case';
import { CompleteIdentityVitalsUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-identity-vitals.use-case';
import { LoadClubsUseCase, LoadGendersUseCase, LoadBloodTypesUseCase } from '@features/reference';
import { LoadFitnessAssessmentsUseCase } from '@features/reference/domain/usecases/load-fitness-assessments.use-case';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';

const PREFILL = { uid: 'SW-1', nameEn: 'Sam', nameAr: 'سام', genderId: 'g1', dob: '2010-01-01', trainingClubId: 'c1' };
const LOOKUP = [{ id: 'x', nameEn: 'X', nameAr: null }];

function build(overrides: { prefill?: unknown; complete?: unknown } = {}) {
  const getPrefill = { run: jest.fn().mockResolvedValue(ok(PREFILL)) };
  const complete = { run: jest.fn().mockResolvedValue(overrides.complete ?? ok(undefined)) };
  if (overrides.prefill) getPrefill.run.mockResolvedValue(overrides.prefill);
  const lookups = { run: jest.fn().mockResolvedValue(ok(LOOKUP)) };
  const auth = { markOnboardingComplete: jest.fn() } as unknown as AuthSessionStore;
  const router = { navigate: jest.fn() } as unknown as Router;
  const notify = { success: jest.fn(), error: jest.fn() } as unknown as NotificationService;
  const i18n = { t: (k: string) => k } as unknown as TranslateService;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      OnboardingViewModel,
      { provide: GetOnboardingPrefillUseCase, useValue: getPrefill },
      { provide: CompleteIdentityVitalsUseCase, useValue: complete },
      { provide: LoadClubsUseCase, useValue: lookups },
      { provide: LoadGendersUseCase, useValue: lookups },
      { provide: LoadBloodTypesUseCase, useValue: lookups },
      { provide: LoadFitnessAssessmentsUseCase, useValue: lookups },
      { provide: AuthSessionStore, useValue: auth },
      { provide: Router, useValue: router },
      { provide: NotificationService, useValue: notify },
      { provide: TranslateService, useValue: i18n },
    ],
  });
  return { vm: TestBed.inject(OnboardingViewModel), complete, auth, router, notify };
}

function fillValid(vm: OnboardingViewModel) {
  vm.examDate.set('2026-01-01'); vm.internalMedId.set('f1'); vm.heartAssessId.set('f1'); vm.spineAssessId.set('f1');
  vm.hemoglobin.set('14.5'); vm.heightCm.set('175'); vm.weightKg.set('68');
}

describe('OnboardingViewModel', () => {
  it('load() seeds identity drafts and lookups', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.nameEn()).toBe('Sam');
    expect(vm.genderId()).toBe('g1');
    expect(vm.trainingClubId()).toBe('c1');
    expect(vm.genders().length).toBe(1);
  });

  it('canSubmit is false until required fields are set', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.canSubmit()).toBe(false);
    fillValid(vm);
    expect(vm.canSubmit()).toBe(true);
  });

  it('submit() success marks onboarding complete and navigates to /home', async () => {
    const { vm, complete, auth, router, notify } = build();
    await vm.load();
    fillValid(vm);
    await vm.submit();
    expect(complete.run).toHaveBeenCalledWith(expect.objectContaining({ nameEn: 'Sam', nameAr: 'سام', bloodTypeId: null, hemoglobin: 14.5 }));
    expect(auth.markOnboardingComplete).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/home']);
    expect(notify.success).toHaveBeenCalled();
  });

  it('submit() failure toasts an error and does not navigate', async () => {
    const { vm, auth, router, notify } = build({ complete: fail(new AppError('nope', 'network')) });
    await vm.load();
    fillValid(vm);
    await vm.submit();
    expect(auth.markOnboardingComplete).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    expect(notify.error).toHaveBeenCalled();
  });
});
