import { TestBed } from '@angular/core/testing';
import { CreateMedicalTestUseCase } from '@features/medical-tests/domain/usecases/create-medical-test.use-case';
import { MEDICAL_TEST_REPOSITORY, IMedicalTestRepository } from '@features/medical-tests/domain/repositories/medical-test.repository';

const RQ = { nameEn: 'Vit D', nameAr: 'فيتامين د', unit: 'ng/mL', lowerBound: 30, upperBound: 100 };

function build(repo: IMedicalTestRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: MEDICAL_TEST_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CreateMedicalTestUseCase);
}

describe('CreateMedicalTestUseCase', () => {
  it('maps the created DTO to a model', async () => {
    const repo = { create: async () => ({ data: { id: 'n1', ...RQ, createdAt: '2026-09-18T00:00:00Z' } }) } as unknown as IMedicalTestRepository;
    const uc = build(repo);
    const r = await uc.run(RQ);
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data.id).toBe('n1');
  });

  it('fails validation when the response is malformed', async () => {
    const repo = { create: async () => ({ data: { id: 'n1' } }) } as unknown as IMedicalTestRepository;
    const uc = build(repo);
    const r = await uc.run(RQ);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
