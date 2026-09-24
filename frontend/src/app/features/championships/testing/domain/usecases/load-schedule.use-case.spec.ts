import { TestBed } from '@angular/core/testing';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { LoadScheduleUseCase } from '@features/championships/domain/usecases/load-schedule.use-case';

function setup(getSchedule: jest.Mock) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [LoadScheduleUseCase, { provide: CHAMPIONSHIPS_REPOSITORY, useValue: { getSchedule } }],
  });
  return TestBed.inject(LoadScheduleUseCase);
}

describe('LoadScheduleUseCase', () => {
  it('maps a valid schedule tree to key-less data', async () => {
    const getSchedule = jest.fn().mockResolvedValue({
      data: { days: [{ id: 'd1', labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
        races: [{ id: 'r1', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00:00', swimmerIds: ['s1'] }] }] },
    });
    const res = await setup(getSchedule).run('e1');
    expect(res.ok).toBe(true);
    expect(res.ok && res.data[0].races[0].scheduledTime).toBe('09:00');
  });

  it('fails on a malformed payload', async () => {
    const getSchedule = jest.fn().mockResolvedValue({ data: { days: [{ id: 'd1' }] } });
    const res = await setup(getSchedule).run('e1');
    expect(res.ok).toBe(false);
  });
});
