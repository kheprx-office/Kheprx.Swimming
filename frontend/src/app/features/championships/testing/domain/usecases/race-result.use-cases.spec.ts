import { TestBed } from '@angular/core/testing';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { LoadRaceScheduleUseCase } from '@features/championships/domain/usecases/load-race-schedule.use-case';
import { LoadResultsUseCase } from '@features/championships/domain/usecases/load-results.use-case';
import { SaveRaceResultsUseCase } from '@features/championships/domain/usecases/save-race-results.use-case';

const scheduleDto = {
  data: { days: [{ id: 'd1', labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
    races: [{ id: 'r1', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00:00', swimmerIds: ['s1'] }] }] },
};
const resultsDto = { data: { results: [{ raceSessionId: 'r1', swimmerId: 's1', timeMs: 24560, points: 0, isPersonalBest: true }] } };

function make<T>(token: any, repo: any): T {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: CHAMPIONSHIPS_REPOSITORY, useValue: repo }] });
  return TestBed.inject(token);
}

describe('race-result use-cases', () => {
  it('LoadRaceSchedule preserves day/race ids and normalizes time to HH:mm', async () => {
    const uc = make<LoadRaceScheduleUseCase>(LoadRaceScheduleUseCase, { getSchedule: jest.fn().mockResolvedValue(scheduleDto) });
    const res = await uc.run('e1');
    expect(res.ok).toBe(true);
    if (res.ok) {
      expect(res.data[0].id).toBe('d1');
      expect(res.data[0].races[0].id).toBe('r1');           // id preserved (unlike LoadScheduleUseCase)
      expect(res.data[0].races[0].scheduledTime).toBe('09:00');
    }
  });

  it('LoadRaceSchedule fails on an invalid payload', async () => {
    const uc = make<LoadRaceScheduleUseCase>(LoadRaceScheduleUseCase, { getSchedule: jest.fn().mockResolvedValue({ data: { days: 'x' } }) });
    const res = await uc.run('e1');
    expect(res.ok).toBe(false);
  });

  it('LoadResults maps rows', async () => {
    const uc = make<LoadResultsUseCase>(LoadResultsUseCase, { getResults: jest.fn().mockResolvedValue(resultsDto) });
    const res = await uc.run('e1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data[0].timeMs).toBe(24560);
  });

  it('LoadResults fails on an invalid payload', async () => {
    const uc = make<LoadResultsUseCase>(LoadResultsUseCase, { getResults: jest.fn().mockResolvedValue({ data: {} }) });
    const res = await uc.run('e1');
    expect(res.ok).toBe(false);
  });

  it('SaveRaceResults sends entries and adopts the returned results', async () => {
    const setRaceResults = jest.fn().mockResolvedValue(resultsDto);
    const uc = make<SaveRaceResultsUseCase>(SaveRaceResultsUseCase, { setRaceResults });
    const res = await uc.run({ eventId: 'e1', raceSessionId: 'r1', entries: [{ swimmerId: 's1', timeMs: 24560 }] });
    expect(setRaceResults).toHaveBeenCalledWith('e1', 'r1', { entries: [{ swimmerId: 's1', timeMs: 24560 }] });
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data[0].swimmerId).toBe('s1');
  });
});
