import { TestBed } from '@angular/core/testing';
import { GetSwimmerGuardiansUseCase } from '@features/swimmer-profile/domain/usecases/get-swimmer-guardians.use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

describe('GetSwimmerGuardiansUseCase', () => {
  const repo = { getGuardians: jest.fn() } as any;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [GetSwimmerGuardiansUseCase, { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }],
    });
  });

  it('maps a valid response to the domain model', async () => {
    repo.getGuardians.mockResolvedValue({ successStatus: true, data: {
      father: { id: 'f1', relationCode: 'father', name: 'Hassan Ali', nationalId: '27001010123456', phone: '+201009876543' },
      mother: null } });
    const res = await TestBed.inject(GetSwimmerGuardiansUseCase).run('sw1');
    expect(res.ok).toBe(true);
    if (res.ok) { expect(res.data.father?.name).toBe('Hassan Ali'); expect(res.data.mother).toBeNull(); }
  });

  it('fails on an invalid response', async () => {
    repo.getGuardians.mockResolvedValue({ successStatus: true, data: { father: { id: 5 } } });
    const res = await TestBed.inject(GetSwimmerGuardiansUseCase).run('sw1');
    expect(res.ok).toBe(false);
  });
});
