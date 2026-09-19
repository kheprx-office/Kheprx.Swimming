import { TestBed } from '@angular/core/testing';
import { GetSwimmerProfileUseCase } from '@features/swimmer-profile/domain/usecases/get-swimmer-profile.use-case';
import { SWIMMER_PROFILE_REPOSITORY, ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

const IDENTITY = { id: 's1', uid: 'SW-1', nameEn: 'Ahmed', nameAr: 'أحمد', dob: '2010-01-01', age: 16, genderCode: 'male', phone: '01000000001', trainingClubNameEn: 'Oasis', trainingClubNameAr: null };
const REF = { id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' };
const VITALS = { id: 'e1', examDate: '2026-09-19', bloodType: null, hemoglobin: 14.8, heightCm: 182, weightKg: 74, internalMed: REF, heartAssess: REF, spineAssess: REF };

function build(repo: Partial<ISwimmerProfileRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(GetSwimmerProfileUseCase);
}

describe('GetSwimmerProfileUseCase', () => {
  it('maps identity and vitals (with null blood type)', async () => {
    const repo = { getProfile: async () => ({ data: { identity: IDENTITY, vitals: VITALS } }) };
    const r = await build(repo as ISwimmerProfileRepository).run('s1');
    expect(r.ok).toBe(true);
    if (r.ok) {
      expect(r.data.identity.nameEn).toBe('Ahmed');
      expect(r.data.vitals?.bloodType).toBeNull();
      expect(r.data.vitals?.internalMed.nameEn).toBe('Fit');
    }
  });

  it('maps a profile with null vitals', async () => {
    const repo = { getProfile: async () => ({ data: { identity: IDENTITY, vitals: null } }) };
    const r = await build(repo as ISwimmerProfileRepository).run('s1');
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data.vitals).toBeNull();
  });

  it('fails validation when identity is malformed', async () => {
    const repo = { getProfile: async () => ({ data: { identity: { id: 's1' }, vitals: null } }) };
    const r = await build(repo as unknown as ISwimmerProfileRepository).run('s1');
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
