import { TestBed } from '@angular/core/testing';
import { GetMySwimmerIdUseCase } from '@features/swimmer-profile/domain/usecases/get-my-swimmer-id.use-case';
import { SWIMMER_PROFILE_REPOSITORY, ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

function build(repo: Partial<ISwimmerProfileRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(GetMySwimmerIdUseCase);
}

describe('GetMySwimmerIdUseCase', () => {
  it('returns the swimmer id from GET /api/swimmers/me', async () => {
    const uc = build({ getMySwimmerId: async () => ({ data: { swimmerId: 'SW-42' } }) } as unknown as ISwimmerProfileRepository);
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data).toBe('SW-42');
  });

  it('fails when the response is malformed', async () => {
    const uc = build({ getMySwimmerId: async () => ({ data: {} }) } as unknown as ISwimmerProfileRepository);
    const r = await uc.run();
    expect(r.ok).toBe(false);
  });
});
