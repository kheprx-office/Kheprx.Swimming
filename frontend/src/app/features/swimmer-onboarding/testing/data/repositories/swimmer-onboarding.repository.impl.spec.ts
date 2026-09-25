import { TestBed } from '@angular/core/testing';
import { SwimmerOnboardingRepositoryImpl } from '@features/swimmer-onboarding/data/repositories/swimmer-onboarding.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('SwimmerOnboardingRepositoryImpl', () => {
  const http = { get: jest.fn(), post: jest.fn() } as unknown as HttpClientService;
  let repo: SwimmerOnboardingRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({ providers: [SwimmerOnboardingRepositoryImpl, { provide: HttpClientService, useValue: http }] });
    repo = TestBed.inject(SwimmerOnboardingRepositoryImpl);
  });

  it('getPrefill GETs the me onboarding endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: {} });
    await repo.getPrefill();
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/me/onboarding/identity-vitals');
  });

  it('completeIdentityVitals POSTs the me onboarding endpoint with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: { mustChangePassword: false } });
    const rq = { nameEn: 'Sam', nameAr: null, genderId: 'g1', dob: '2010-01-01', trainingClubId: 'c1', examDate: '2026-01-01', bloodTypeId: null, hemoglobin: 14.5, heightCm: 175, weightKg: 68, internalMedId: 'f1', heartAssessId: 'f1', spineAssessId: 'f1', phone: null };
    await repo.completeIdentityVitals(rq);
    expect(http.post).toHaveBeenCalledWith('/api/swimmers/me/onboarding/identity-vitals', { body: rq });
  });
});
