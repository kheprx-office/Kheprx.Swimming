import { TestBed } from '@angular/core/testing';
import { ListInBodyReadingsUseCase } from '@features/swimmer-profile/domain/usecases/list-inbody-readings.use-case';
import { CreateInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/create-inbody-reading.use-case';
import { UpdateInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/update-inbody-reading.use-case';
import { DeleteInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/delete-inbody-reading.use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

const READING = { id: 'r1', readingDate: '2024-10-04', heightCm: 180, weightKg: 74, fatPct: 12.8, musclePct: 42.1, boneDensity: 1.35, bodyDensity: 1.07, recordedBy: 'u1' };
const RQ = { readingDate: '2024-10-04', heightCm: 180, weightKg: 74, fatPct: 12.8, musclePct: 42.1, boneDensity: 1.35, bodyDensity: 1.07 };

describe('inbody use cases', () => {
  const repo = { getInBodyReadings: jest.fn(), createInBodyReading: jest.fn(), updateInBodyReading: jest.fn(), deleteInBodyReading: jest.fn() } as any;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [
      ListInBodyReadingsUseCase, CreateInBodyReadingUseCase, UpdateInBodyReadingUseCase, DeleteInBodyReadingUseCase,
      { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo },
    ] });
  });

  it('list maps valid readings', async () => {
    repo.getInBodyReadings.mockResolvedValue({ successStatus: true, data: [READING] });
    const res = await TestBed.inject(ListInBodyReadingsUseCase).run('sw1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data[0].weightKg).toBe(74);
  });

  it('list fails on invalid payload', async () => {
    repo.getInBodyReadings.mockResolvedValue({ successStatus: true, data: [{ id: 5 }] });
    const res = await TestBed.inject(ListInBodyReadingsUseCase).run('sw1');
    expect(res.ok).toBe(false);
  });

  it('create maps the returned reading', async () => {
    repo.createInBodyReading.mockResolvedValue({ successStatus: true, data: READING });
    const res = await TestBed.inject(CreateInBodyReadingUseCase).run({ id: 'sw1', rq: RQ });
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data.id).toBe('r1');
    expect(repo.createInBodyReading).toHaveBeenCalledWith('sw1', RQ);
  });

  it('update maps the returned reading', async () => {
    repo.updateInBodyReading.mockResolvedValue({ successStatus: true, data: READING });
    const res = await TestBed.inject(UpdateInBodyReadingUseCase).run({ id: 'sw1', readingId: 'r1', rq: RQ });
    expect(res.ok).toBe(true);
    expect(repo.updateInBodyReading).toHaveBeenCalledWith('sw1', 'r1', RQ);
  });

  it('delete calls the repo', async () => {
    repo.deleteInBodyReading.mockResolvedValue({ successStatus: true, data: null });
    const res = await TestBed.inject(DeleteInBodyReadingUseCase).run({ id: 'sw1', readingId: 'r1' });
    expect(res.ok).toBe(true);
    expect(repo.deleteInBodyReading).toHaveBeenCalledWith('sw1', 'r1');
  });
});
