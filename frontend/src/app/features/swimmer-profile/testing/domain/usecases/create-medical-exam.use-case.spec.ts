import { TestBed } from '@angular/core/testing';
import { CreateMedicalExamUseCase } from '@features/swimmer-profile/domain/usecases/create-medical-exam.use-case';
import { SWIMMER_PROFILE_REPOSITORY, ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

const REF = { id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' };
const VITALS = { id: 'e1', examDate: '2026-09-19', bloodType: null, hemoglobin: 15, heightCm: 183, weightKg: 75, internalMed: REF, heartAssess: REF, spineAssess: REF };

function build(repo: Partial<ISwimmerProfileRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CreateMedicalExamUseCase);
}

describe('CreateMedicalExamUseCase', () => {
  it('maps the created vitals', async () => {
    const uc = build({ createExam: async () => ({ data: VITALS }) } as unknown as ISwimmerProfileRepository);
    const rq = { examDate: '2026-09-19', hemoglobin: 15, heightCm: 183, weightKg: 75, internalMedId: 'f1', heartAssessId: 'f1', spineAssessId: 'f1' };
    const r = await uc.run({ id: 's1', rq });
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data.hemoglobin).toBe(15); expect(r.data.internalMed.code).toBe('fit'); }
  });
});
