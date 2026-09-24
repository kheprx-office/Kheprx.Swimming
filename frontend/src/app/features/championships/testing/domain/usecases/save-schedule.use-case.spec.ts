import { TestBed } from '@angular/core/testing';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { SaveScheduleUseCase } from '@features/championships/domain/usecases/save-schedule.use-case';
import { ScheduleDayData } from '@features/championships/domain/model/competition-schedule';

const days: ScheduleDayData[] = [
  { labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
    races: [{ strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1'] }] },
];

function setup(setSchedule: jest.Mock) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [SaveScheduleUseCase, { provide: CHAMPIONSHIPS_REPOSITORY, useValue: { setSchedule } }],
  });
  return TestBed.inject(SaveScheduleUseCase);
}

describe('SaveScheduleUseCase', () => {
  it('PUTs the mapped request and returns the re-read tree', async () => {
    const setSchedule = jest.fn().mockResolvedValue({
      data: { days: [{ id: 'D1', labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
        races: [{ id: 'R1', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00:00', swimmerIds: ['s1'] }] }] },
    });
    const res = await setup(setSchedule).run({ eventId: 'e1', days });

    expect(setSchedule).toHaveBeenCalledWith('e1', {
      days: [{ labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
        races: [{ strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1'] }] }],
    });
    expect(res.ok).toBe(true);
    expect(res.ok && res.data[0].races[0].scheduledTime).toBe('09:00');
  });

  it('fails when the server returns a malformed tree', async () => {
    const setSchedule = jest.fn().mockResolvedValue({ data: { days: [{ id: 'D1' }] } });
    const res = await setup(setSchedule).run({ eventId: 'e1', days });
    expect(res.ok).toBe(false);
  });
});
