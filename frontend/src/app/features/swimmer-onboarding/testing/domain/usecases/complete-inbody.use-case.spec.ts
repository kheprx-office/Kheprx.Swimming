import { TestBed } from '@angular/core/testing';
import { CompleteInBodyUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-inbody.use-case';
import { SWIMMER_ONBOARDING_REPOSITORY, ISwimmerOnboardingRepository } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { InBodySubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

const SUBMISSION: InBodySubmission = { heightCm: 175, weightKg: 68, fatPct: 15, musclePct: 40, waterPct: 55, boneDensity: 3.2, bodyDensity: 1.05 };

function build(repo: Partial<ISwimmerOnboardingRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_ONBOARDING_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CompleteInBodyUseCase);
}

describe('CompleteInBodyUseCase', () => {
  it('posts the submission and succeeds', async () => {
    const post = jest.fn().mockResolvedValue({ data: { mustChangePassword: false } });
    const uc = build({ completeInBody: post } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(true);
    expect(post).toHaveBeenCalledWith({ heightCm: 175, weightKg: 68, fatPct: 15, musclePct: 40, waterPct: 55, boneDensity: 3.2, bodyDensity: 1.05 });
  });

  it('fails when the repository throws', async () => {
    const uc = build({ completeInBody: async () => { throw new Error('boom'); } } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(false);
  });
});
