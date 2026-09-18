import { TestBed } from '@angular/core/testing';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { SWIMMER_REPOSITORY, ISwimmerRepository } from '@features/swimmers/domain/repositories/swimmer.repository';

function makeRepo(overrides: Partial<ISwimmerRepository> = {}): ISwimmerRepository {
  return {
    list: async () => ({ data: [
      { id: '1', uid: 'SW-0001', nameEn: 'Alpha', nameAr: 'ألفا', clubNameEn: 'Oasis Main', clubNameAr: null, genderCode: 'male', age: 15 },
    ] }),
    ...overrides,
  } as unknown as ISwimmerRepository;
}

function build(repo: ISwimmerRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_REPOSITORY, useValue: repo }] });
  return TestBed.inject(ListSwimmersUseCase);
}

describe('ListSwimmersUseCase', () => {
  it('maps DTOs to domain models', async () => {
    const uc = build(makeRepo());
    const r = await uc.run(undefined);
    expect(r.ok).toBe(true);
    if (r.ok) {
      expect(r.data).toHaveLength(1);
      expect(r.data[0].uid).toBe('SW-0001');
      expect(r.data[0].gender).toBe('male');
      expect(r.data[0].age).toBe(15);
    }
  });

  it('drops malformed items and coerces unknown gender to null', async () => {
    const uc = build(makeRepo({ list: async () => ({ data: [
      { id: '2', uid: 'SW-0002', nameEn: 'Bravo', genderCode: '', age: null } as never,
      { uid: 'broken' } as never,
    ] }) }));
    const r = await uc.run(undefined);
    expect(r.ok).toBe(true);
    if (r.ok) {
      expect(r.data).toHaveLength(1);
      expect(r.data[0].gender).toBeNull();
      expect(r.data[0].age).toBeNull();
    }
  });
});
