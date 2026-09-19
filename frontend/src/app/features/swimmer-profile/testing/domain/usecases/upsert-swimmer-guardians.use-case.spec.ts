import { TestBed } from '@angular/core/testing';
import { UpsertSwimmerGuardiansUseCase } from '@features/swimmer-profile/domain/usecases/upsert-swimmer-guardians.use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

describe('UpsertSwimmerGuardiansUseCase', () => {
  const repo = { upsertGuardians: jest.fn() } as any;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [UpsertSwimmerGuardiansUseCase, { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }],
    });
  });

  it('calls the repository and succeeds', async () => {
    repo.upsertGuardians.mockResolvedValue({ successStatus: true, data: null });
    const rq = {
      father: { name: 'Hassan Ali', nationalId: '27001010123456', phone: '+201009876543' },
      mother: { name: 'Fatima Ibrahim', nationalId: '27505050123456', phone: '+201005554444' },
    };
    const res = await TestBed.inject(UpsertSwimmerGuardiansUseCase).run({ id: 'sw1', rq });
    expect(res.ok).toBe(true);
    expect(repo.upsertGuardians).toHaveBeenCalledWith('sw1', rq);
  });
});
