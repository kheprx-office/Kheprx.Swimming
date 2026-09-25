import { TestBed } from '@angular/core/testing';
import { CompleteGuardianMedicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-guardian-medical.use-case';
import { SWIMMER_ONBOARDING_REPOSITORY, ISwimmerOnboardingRepository } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { GuardianMedicalSubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

const SUBMISSION: GuardianMedicalSubmission = {
  father: { name: 'Ahmed', nationalId: '12345678901234', phone: '010' },
  mother: { name: 'Sara', nationalId: '43210987654321', phone: '011' },
  medical: [{ categoryId: 'cat-allergy', fieldLabel: 'Allergies', value: 'Peanuts' }],
};

function build(repo: Partial<ISwimmerOnboardingRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_ONBOARDING_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CompleteGuardianMedicalUseCase);
}

describe('CompleteGuardianMedicalUseCase', () => {
  it('posts the submission and succeeds', async () => {
    const post = jest.fn().mockResolvedValue({ data: { mustChangePassword: false } });
    const uc = build({ completeGuardianMedical: post } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(true);
    expect(post).toHaveBeenCalledWith(expect.objectContaining({
      father: expect.objectContaining({ name: 'Ahmed', nationalId: '12345678901234' }),
      medical: [{ categoryId: 'cat-allergy', fieldLabel: 'Allergies', value: 'Peanuts' }],
    }));
  });

  it('fails when the repository throws', async () => {
    const uc = build({ completeGuardianMedical: async () => { throw new Error('boom'); } } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(false);
  });
});
