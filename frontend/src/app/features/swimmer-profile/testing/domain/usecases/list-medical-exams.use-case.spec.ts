import { TestBed } from '@angular/core/testing';
import { ListMedicalExamsUseCase } from '@features/swimmer-profile/domain/usecases/list-medical-exams.use-case';
import { SWIMMER_PROFILE_REPOSITORY, ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

const REF = { id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' };
const DTO = { id: 'e1', examDate: '2026-09-19', bloodType: null, hemoglobin: 15, heightCm: 183, weightKg: 75, internalMed: REF, heartAssess: REF, spineAssess: REF };

function build(repo: Partial<ISwimmerProfileRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(ListMedicalExamsUseCase);
}

describe('ListMedicalExamsUseCase', () => {
  it('maps the exam list', async () => {
    const uc = build({ listExams: async () => ({ data: [DTO] }) } as unknown as ISwimmerProfileRepository);
    const r = await uc.run('s1');
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data).toHaveLength(1); expect(r.data[0].id).toBe('e1'); }
  });
  it('fails validation on a malformed payload', async () => {
    const uc = build({ listExams: async () => ({ data: [{ id: 'e1' }] }) } as unknown as ISwimmerProfileRepository);
    const r = await uc.run('s1');
    expect(r.ok).toBe(false);
  });
});
