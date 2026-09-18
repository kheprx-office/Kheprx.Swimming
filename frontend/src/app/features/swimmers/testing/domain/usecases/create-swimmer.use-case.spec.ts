import { TestBed } from '@angular/core/testing';
import { CreateSwimmerUseCase } from '@features/swimmers/domain/usecases/create-swimmer.use-case';
import { SWIMMER_REPOSITORY, ISwimmerRepository } from '@features/swimmers/domain/repositories/swimmer.repository';

const OK = { data: { id: 'x', uid: 'SW-0007', username: 'mona.ali', nameEn: 'Mona Ali', temporaryPassword: 'Oasis2026!' } };
const rq = { nameEn: 'Mona Ali', username: 'mona.ali', trainingClubId: 'c1', genderId: 'g1', dob: '2010-05-01', bloodTypeId: 'b1', strokeIds: ['s1'] };

function build(repo: ISwimmerRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CreateSwimmerUseCase);
}

describe('CreateSwimmerUseCase', () => {
  it('maps the created swimmer', async () => {
    const uc = build({ getCount: async () => ({ data: { count: 0 } }), create: async () => OK } as unknown as ISwimmerRepository);
    const r = await uc.run(rq as never);
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data.uid).toBe('SW-0007'); expect(r.data.temporaryPassword).toBe('Oasis2026!'); }
  });

  it('fails validation on malformed response', async () => {
    const uc = build({ getCount: async () => ({ data: { count: 0 } }), create: async () => ({ data: { uid: 'SW-0007' } }) } as unknown as ISwimmerRepository);
    const r = await uc.run(rq as never);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
