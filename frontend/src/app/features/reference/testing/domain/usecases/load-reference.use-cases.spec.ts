import { TestBed } from '@angular/core/testing';
import { LoadClubsUseCase } from '@features/reference/domain/usecases/load-clubs.use-case';
import { LoadStrokesUseCase } from '@features/reference/domain/usecases/load-strokes.use-case';
import { REFERENCE_REPOSITORY, IReferenceRepository } from '@features/reference/domain/repositories/reference.repository';

function makeRepo(overrides: Partial<IReferenceRepository> = {}): IReferenceRepository {
  return {
    getClubs: async () => ({ data: [{ id: 'c1', nameEn: 'Al Ahly', nameAr: 'الأهلي' }] }),
    getBloodTypes: async () => ({ data: [{ id: 'b1', code: 'O+', nameEn: 'O+', nameAr: 'O+' }] }),
    getStrokes: async () => ({ data: [{ id: 's1', code: 'medley', nameEn: 'IM', nameAr: null }] }),
    getGenders: async () => ({ data: [{ id: 'g1', code: 'male', nameEn: 'Male', nameAr: 'ذكر' }] }),
    ...overrides,
  } as unknown as IReferenceRepository;
}

function build<T>(type: new (...args: never[]) => T, repo: IReferenceRepository): T {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: REFERENCE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(type);
}

describe('reference load use-cases', () => {
  it('LoadClubs maps to LookupItem[] with no code', async () => {
    const uc = build(LoadClubsUseCase, makeRepo());
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) {
      expect(r.data[0].id).toBe('c1');
      expect(r.data[0].nameEn).toBe('Al Ahly');
      expect(r.data[0].code).toBeUndefined();
    }
  });

  it('LoadStrokes carries the stroke code through', async () => {
    const uc = build(LoadStrokesUseCase, makeRepo());
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data[0].code).toBe('medley');
  });

  it('LoadClubs fails validation on malformed data', async () => {
    const uc = build(LoadClubsUseCase, makeRepo({ getClubs: async () => ({ data: [{ nameEn: 'x', nameAr: null }] }) as never }));
    const r = await uc.run();
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
