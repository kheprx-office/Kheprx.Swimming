import { TestBed } from '@angular/core/testing';
import { LoadFitnessAssessmentsUseCase } from '@features/reference/domain/usecases/load-fitness-assessments.use-case';
import { REFERENCE_REPOSITORY, IReferenceRepository } from '@features/reference/domain/repositories/reference.repository';

function build(repo: Partial<IReferenceRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: REFERENCE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(LoadFitnessAssessmentsUseCase);
}

describe('LoadFitnessAssessmentsUseCase', () => {
  it('maps the coded lookup list to LookupItems', async () => {
    const repo = { getFitnessAssessments: async () => ({ data: [{ id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' }] }) };
    const r = await build(repo as IReferenceRepository).run();
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data).toHaveLength(1); expect(r.data[0].code).toBe('fit'); }
  });

  it('fails validation when the payload is malformed', async () => {
    const repo = { getFitnessAssessments: async () => ({ data: [{ id: '' }] }) };
    const r = await build(repo as unknown as IReferenceRepository).run();
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
