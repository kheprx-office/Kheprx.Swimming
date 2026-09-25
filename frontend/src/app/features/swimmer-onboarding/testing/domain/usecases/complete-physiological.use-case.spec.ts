import { TestBed } from '@angular/core/testing';
import { CompletePhysiologicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-physiological.use-case';
import { SWIMMER_ONBOARDING_REPOSITORY, ISwimmerOnboardingRepository } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { PhysiologicalSubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

const SUBMISSION: PhysiologicalSubmission = { rightArmCm: 32.5, leftArmCm: 31, rightLegCm: 95, leftLegCm: 94, torsoCm: 60, bustDiameterCm: 90, waistDiameterCm: 75 };

function build(repo: Partial<ISwimmerOnboardingRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_ONBOARDING_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CompletePhysiologicalUseCase);
}

describe('CompletePhysiologicalUseCase', () => {
  it('posts the submission and succeeds', async () => {
    const post = jest.fn().mockResolvedValue({ data: { mustChangePassword: false } });
    const uc = build({ completePhysiological: post } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(true);
    expect(post).toHaveBeenCalledWith({ rightArmCm: 32.5, leftArmCm: 31, rightLegCm: 95, leftLegCm: 94, torsoCm: 60, bustDiameterCm: 90, waistDiameterCm: 75 });
  });

  it('fails when the repository throws', async () => {
    const uc = build({ completePhysiological: async () => { throw new Error('boom'); } } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(false);
  });
});
