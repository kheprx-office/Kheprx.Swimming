import { TestBed } from '@angular/core/testing';
import { CreateObservationUseCase } from '@features/observations/domain/usecases/create-observation.use-case';
import { OBSERVATION_REPOSITORY, IObservationRepository } from '@features/observations/domain/repositories/observation.repository';

const RQ = { swimmerId: 's1', categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe' };
const DTO = { id: 'o1', swimmerId: 's1', categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe', observedDate: '2026-09-18T00:00:00Z', recordedBy: 'u1' };

function build(repo: IObservationRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: OBSERVATION_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CreateObservationUseCase);
}

describe('CreateObservationUseCase', () => {
  it('maps the created DTO to a model', async () => {
    const repo = { create: async () => ({ data: DTO }) } as unknown as IObservationRepository;
    const uc = build(repo);
    const r = await uc.run(RQ);
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data.id).toBe('o1');
  });

  it('fails validation when the response is malformed', async () => {
    const repo = { create: async () => ({ data: { id: 'o1' } }) } as unknown as IObservationRepository;
    const uc = build(repo);
    const r = await uc.run(RQ);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
