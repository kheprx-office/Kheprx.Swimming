import { TestBed } from '@angular/core/testing';
import { LoadFeedbackCategoriesUseCase } from '@features/reference/domain/usecases/load-feedback-categories.use-case';
import { REFERENCE_REPOSITORY, IReferenceRepository } from '@features/reference/domain/repositories/reference.repository';

function build(repo: IReferenceRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: REFERENCE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(LoadFeedbackCategoriesUseCase);
}

describe('LoadFeedbackCategoriesUseCase', () => {
  it('maps coded lookups to LookupItems', async () => {
    const repo = { getFeedbackCategories: async () => ({ data: [{ id: 'c1', code: 'technique', nameEn: 'Technique', nameAr: 'الأداء الفني' }] }) } as unknown as IReferenceRepository;
    const uc = build(repo);
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data[0].id).toBe('c1'); expect(r.data[0].code).toBe('technique'); }
  });

  it('fails validation on malformed data', async () => {
    const repo = { getFeedbackCategories: async () => ({ data: [{ id: 'c1' }] }) } as unknown as IReferenceRepository;
    const uc = build(repo);
    const r = await uc.run();
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
