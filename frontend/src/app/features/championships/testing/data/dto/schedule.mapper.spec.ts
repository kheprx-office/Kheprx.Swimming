import { ScheduleDtoData } from '@features/championships/data/dto/schedule.dto';
import { toScheduleDataList, toSetScheduleRq } from '@features/championships/data/dto/schedule.mapper';
import { ScheduleDayData } from '@features/championships/domain/model/competition-schedule';

describe('schedule mapper', () => {
  it('reads a DTO tree and normalizes the time to HH:mm', () => {
    const dto: ScheduleDtoData = {
      days: [
        {
          id: 'day1', labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
          races: [
            { id: 'r1', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00:00', swimmerIds: ['s1', 's2'] },
          ],
        },
      ],
    };

    const data = toScheduleDataList(dto);

    expect(data).toHaveLength(1);
    expect(data[0].labelEn).toBe('Day 1');
    expect(data[0].races[0].scheduledTime).toBe('09:00'); // sliced from HH:mm:ss
    expect(data[0].races[0].swimmerIds).toEqual(['s1', 's2']);
  });

  it('builds a SetScheduleRq that drops client keys', () => {
    const data: ScheduleDayData[] = [
      { labelEn: 'Day 1', labelAr: 'يوم', dayDate: '2023-11-15',
        races: [{ strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1'] }] },
    ];

    const rq = toSetScheduleRq(data);

    expect(rq).toEqual({
      days: [
        { labelEn: 'Day 1', labelAr: 'يوم', dayDate: '2023-11-15',
          races: [{ strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1'] }] },
      ],
    });
  });
});
