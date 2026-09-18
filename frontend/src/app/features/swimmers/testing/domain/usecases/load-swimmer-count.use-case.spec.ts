import { TestBed } from '@angular/core/testing';
import { LoadSwimmerCountUseCase } from '@features/swimmers/domain/usecases/load-swimmer-count.use-case';
import { SWIMMER_REPOSITORY, ISwimmerRepository } from '@features/swimmers/domain/repositories/swimmer.repository';

function makeRepo(overrides: Partial<ISwimmerRepository> = {}): ISwimmerRepository {
  return {
    getCount: async () => ({ data: { count: 24 } }),
    ...overrides,
  } as unknown as ISwimmerRepository;
}

function build(repo: ISwimmerRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_REPOSITORY, useValue: repo }] });
  return TestBed.inject(LoadSwimmerCountUseCase);
}

describe('LoadSwimmerCountUseCase', () => {
  it('validates + returns the count', async () => {
    const uc = build(makeRepo());
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data).toBe(24);
  });

  it('returns fail(validation) on a malformed count', async () => {
    const uc = build(makeRepo({ getCount: async () => ({ data: { count: -1 } }) }));
    const r = await uc.run();
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
