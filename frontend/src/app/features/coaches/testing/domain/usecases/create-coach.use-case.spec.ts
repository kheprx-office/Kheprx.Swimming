import { TestBed } from '@angular/core/testing';
import { CreateCoachUseCase } from '@features/coaches/domain/usecases/create-coach.use-case';
import { COACH_REPOSITORY, ICoachRepository } from '@features/coaches/domain/repositories/coach.repository';

const OK = { data: { id: 'x', username: 'dave.coach', nameEn: 'Dave', role: 'captain', temporaryPassword: 'Oasis2026!' } };
const rq = { role: 'captain', nameEn: 'Dave', username: 'dave.coach', email: 'd@o.com', nationalId: '29001011234567', genderId: 'g1', dob: '1990-01-01', phone: '01000000000' };

function build(repo: ICoachRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: COACH_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CreateCoachUseCase);
}

describe('CreateCoachUseCase', () => {
  it('maps the created coach', async () => {
    const uc = build({ create: async () => OK } as unknown as ICoachRepository);
    const r = await uc.run(rq as never);
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data.role).toBe('captain'); expect(r.data.temporaryPassword).toBe('Oasis2026!'); }
  });

  it('fails validation on malformed response', async () => {
    const uc = build({ create: async () => ({ data: { id: 'x' } }) } as unknown as ICoachRepository);
    const r = await uc.run(rq as never);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
