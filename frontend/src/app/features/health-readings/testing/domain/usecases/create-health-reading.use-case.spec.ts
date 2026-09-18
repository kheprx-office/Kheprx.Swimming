import { TestBed } from '@angular/core/testing';
import { CreateHealthReadingUseCase } from '@features/health-readings/domain/usecases/create-health-reading.use-case';
import { HEALTH_READING_REPOSITORY, IHealthReadingRepository } from '@features/health-readings/domain/repositories/health-reading.repository';

const RQ = { swimmerId: 's1', medicalTestId: 't1', value: 95 };
const DTO = { id: 'r1', swimmerId: 's1', medicalTestId: 't1', value: 95, readingDate: '2026-09-18T00:00:00Z', recordedBy: 'u1', status: 'normal' };

function build(repo: IHealthReadingRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: HEALTH_READING_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CreateHealthReadingUseCase);
}

describe('CreateHealthReadingUseCase', () => {
  it('maps the created DTO to a model', async () => {
    const repo = { create: async () => ({ data: DTO }) } as unknown as IHealthReadingRepository;
    const uc = build(repo);
    const r = await uc.run(RQ);
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data.id).toBe('r1'); expect(r.data.status).toBe('normal'); }
  });

  it('fails validation when the response is malformed', async () => {
    const repo = { create: async () => ({ data: { id: 'r1' } }) } as unknown as IHealthReadingRepository;
    const uc = build(repo);
    const r = await uc.run(RQ);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
