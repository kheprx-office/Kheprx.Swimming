import { TestBed } from '@angular/core/testing';
import { GetOnboardingPrefillUseCase } from '@features/swimmer-onboarding/domain/usecases/get-onboarding-prefill.use-case';
import { SWIMMER_ONBOARDING_REPOSITORY, ISwimmerOnboardingRepository } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';

function build(repo: Partial<ISwimmerOnboardingRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_ONBOARDING_REPOSITORY, useValue: repo }] });
  return TestBed.inject(GetOnboardingPrefillUseCase);
}

describe('GetOnboardingPrefillUseCase', () => {
  it('maps the prefill DTO to the domain model', async () => {
    const repo = { getPrefill: async () => ({ data: { uid: 'SW-1', nameEn: 'Sam', nameAr: 'سام', genderId: 'g1', dob: '2010-01-01', trainingClubId: 'c1', phone: '01099999999' } }) };
    const r = await build(repo as ISwimmerOnboardingRepository).run();
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data.nameEn).toBe('Sam'); expect(r.data.trainingClubId).toBe('c1'); expect(r.data.phone).toBe('01099999999'); }
  });

  it('fails validation when the prefill is malformed', async () => {
    const repo = { getPrefill: async () => ({ data: { uid: 5 } }) };
    const r = await build(repo as unknown as ISwimmerOnboardingRepository).run();
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
