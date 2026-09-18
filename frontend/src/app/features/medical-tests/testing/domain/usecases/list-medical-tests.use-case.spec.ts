import { TestBed } from '@angular/core/testing';
import { ListMedicalTestsUseCase } from '@features/medical-tests/domain/usecases/list-medical-tests.use-case';
import { MEDICAL_TEST_REPOSITORY, IMedicalTestRepository } from '@features/medical-tests/domain/repositories/medical-test.repository';

function makeRepo(overrides: Partial<IMedicalTestRepository> = {}): IMedicalTestRepository {
  return {
    list: async () => ({ data: [
      { id: '1', nameEn: 'Hemoglobin', nameAr: 'هيموغلوبين', unit: 'g/dL', lowerBound: 11, upperBound: 17.5, createdAt: '2026-09-18T00:00:00Z' },
    ] }),
    create: async () => ({ data: {} as never }),
    delete: async () => ({ data: null }),
    ...overrides,
  } as unknown as IMedicalTestRepository;
}

function build(repo: IMedicalTestRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: MEDICAL_TEST_REPOSITORY, useValue: repo }] });
  return TestBed.inject(ListMedicalTestsUseCase);
}

describe('ListMedicalTestsUseCase', () => {
  it('maps DTOs to domain models', async () => {
    const uc = build(makeRepo());
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) {
      expect(r.data).toHaveLength(1);
      expect(r.data[0].nameEn).toBe('Hemoglobin');
      expect(r.data[0].upperBound).toBe(17.5);
    }
  });

  it('drops malformed rows', async () => {
    const uc = build(makeRepo({ list: async () => ({ data: [{ id: 'x' } as never] }) }));
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data).toHaveLength(0);
  });
});
