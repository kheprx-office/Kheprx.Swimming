import { TestBed } from '@angular/core/testing';
import { CompleteIdentityVitalsUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-identity-vitals.use-case';
import { SWIMMER_ONBOARDING_REPOSITORY, ISwimmerOnboardingRepository } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { IdentityVitalsSubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

const SUBMISSION: IdentityVitalsSubmission = {
  nameEn: 'Sam', nameAr: null, genderId: 'g1', dob: '2010-01-01', trainingClubId: 'c1',
  examDate: '2026-01-01', bloodTypeId: null, hemoglobin: 14.5, heightCm: 175, weightKg: 68,
  internalMedId: 'f1', heartAssessId: 'f1', spineAssessId: 'f1',
};

function build(repo: Partial<ISwimmerOnboardingRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_ONBOARDING_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CompleteIdentityVitalsUseCase);
}

describe('CompleteIdentityVitalsUseCase', () => {
  it('posts the submission and succeeds', async () => {
    const post = jest.fn().mockResolvedValue({ data: { mustChangePassword: false } });
    const uc = build({ completeIdentityVitals: post } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(true);
    expect(post).toHaveBeenCalledWith(expect.objectContaining({ nameEn: 'Sam', bloodTypeId: null, hemoglobin: 14.5 }));
  });

  it('fails when the repository throws', async () => {
    const uc = build({ completeIdentityVitals: async () => { throw new Error('boom'); } } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(false);
  });
});
