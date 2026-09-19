import { TestBed } from '@angular/core/testing';
import { UpdateSwimmerIdentityUseCase } from '@features/swimmer-profile/domain/usecases/update-swimmer-identity.use-case';
import { SWIMMER_PROFILE_REPOSITORY, ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

function build(repo: Partial<ISwimmerProfileRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(UpdateSwimmerIdentityUseCase);
}

describe('UpdateSwimmerIdentityUseCase', () => {
  it('calls the repository with id and body', async () => {
    const updateIdentity = jest.fn().mockResolvedValue({ data: null });
    const uc = build({ updateIdentity } as unknown as ISwimmerProfileRepository);
    const rq = { nameEn: 'New', nameAr: null, dob: '2010-01-01', phone: '01000000009' };
    const r = await uc.run({ id: 's1', rq });
    expect(r.ok).toBe(true);
    expect(updateIdentity).toHaveBeenCalledWith('s1', rq);
  });

  it('fails when the repository throws', async () => {
    const updateIdentity = jest.fn().mockRejectedValue(new Error('boom'));
    const uc = build({ updateIdentity } as unknown as ISwimmerProfileRepository);
    const r = await uc.run({ id: 's1', rq: { nameEn: 'New', nameAr: null, dob: '2010-01-01', phone: null } });
    expect(r.ok).toBe(false);
  });
});
