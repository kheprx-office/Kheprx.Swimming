import { TestBed } from '@angular/core/testing';
import { ok } from '@core/domain/result/result';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { RaceResultsViewModel } from '@features/championships/presentation/pages/championship-detail/race-results.viewmodel';
import { LoadRaceScheduleUseCase } from '@features/championships/domain/usecases/load-race-schedule.use-case';
import { LoadResultsUseCase } from '@features/championships/domain/usecases/load-results.use-case';
import { SaveRaceResultsUseCase } from '@features/championships/domain/usecases/save-race-results.use-case';
import { LoadStrokesUseCase, LoadDistancesUseCase } from '@features/reference';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { RaceScheduleDay, RaceResultEntry } from '@features/championships/domain/model/race-result';
import { AppError } from '@core/domain/errors/app-error';

const strokes = [{ id: 'st1', code: 'freestyle', nameEn: 'Freestyle', nameAr: null }];
const distances = [{ id: 'ds1', code: '50m', nameEn: '50m', nameAr: null }, { id: 'ds2', code: '100m', nameEn: '100m', nameAr: null }];
const roster = [
  { id: 's1', uid: 'U1', nameEn: 'Ahmed', nameAr: null, clubNameEn: 'Oasis', clubNameAr: null, gender: 'male', age: 18 },
  { id: 's2', uid: 'U2', nameEn: 'Sara', nameAr: null, clubNameEn: 'Oasis', clubNameAr: null, gender: 'female', age: 16 },
];
// Two past races: r1 (50m Free) has 2 swimmers, r2 (100m Free) has 0 swimmers.
const schedule: RaceScheduleDay[] = [{
  id: 'd1', labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
  races: [
    { id: 'r1', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1', 's2'] },
    { id: 'r2', strokeId: 'st1', distanceId: 'ds2', scheduledTime: '10:00', swimmerIds: [] },
  ],
}];
// A far-future race with no results still shows in Finished (no start-time gate).
const futureDay: RaceScheduleDay = {
  id: 'd2', labelEn: 'Day 2', labelAr: null, dayDate: '2999-01-01',
  races: [{ id: 'r3', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1'] }],
};

interface Opts { role?: string; schedule?: RaceScheduleDay[]; results?: RaceResultEntry[]; saveRun?: jest.Mock; }

function setup(opts: Opts = {}) {
  const schedRun = jest.fn().mockResolvedValue(ok(opts.schedule ?? schedule));
  const resultsRun = jest.fn().mockResolvedValue(ok(opts.results ?? []));
  const saveRun = opts.saveRun ?? jest.fn();
  const notify = { success: jest.fn(), error: jest.fn() };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      RaceResultsViewModel,
      { provide: LoadRaceScheduleUseCase, useValue: { run: schedRun } },
      { provide: LoadResultsUseCase, useValue: { run: resultsRun } },
      { provide: SaveRaceResultsUseCase, useValue: { run: saveRun } },
      { provide: LoadStrokesUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(strokes)) } },
      { provide: LoadDistancesUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(distances)) } },
      { provide: ListSwimmersUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(roster)) } },
      { provide: LanguageStore, useValue: { lang: () => 'en' } },
      { provide: AuthSessionStore, useValue: { role: () => opts.role ?? 'head_coach' } },
      { provide: NotificationService, useValue: notify },
      { provide: TranslateService, useValue: { t: (k: string) => k } },
    ],
  });
  return { vm: TestBed.inject(RaceResultsViewModel), schedRun, resultsRun, saveRun, notify };
}

describe('RaceResultsViewModel', () => {
  it('lists past races with no results under Finished, resolving names', async () => {
    const { vm } = setup();
    await vm.load('e1');
    expect(vm.loaded()).toBe(true);
    const finished = vm.finishedRaces();
    expect(finished.map((f) => f.raceSessionId)).toEqual(['r1', 'r2']);
    expect(finished[0].raceName).toBe('50m Freestyle');
    expect(finished[0].swimmers.map((s) => s.name)).toEqual(['Ahmed', 'Sara']);
    expect(finished[1].swimmers).toEqual([]); // r2 has no assigned swimmers
    expect(vm.finishedCount()).toBe(2);
    expect(vm.resultRaces()).toEqual([]);
  });

  it('a future race with no results still appears in Finished', async () => {
    const { vm } = setup({ schedule: [futureDay] });
    await vm.load('e1');
    expect(vm.finishedRaces().map((f) => f.raceSessionId)).toEqual(['r3']);
  });

  it('a race with results appears in Results (ranked by time, PB kept) and leaves Finished', async () => {
    const results: RaceResultEntry[] = [
      { raceSessionId: 'r1', swimmerId: 's2', timeMs: 25890, points: 0, isPersonalBest: false },
      { raceSessionId: 'r1', swimmerId: 's1', timeMs: 24560, points: 0, isPersonalBest: true },
    ];
    const { vm } = setup({ results });
    await vm.load('e1');
    expect(vm.finishedRaces().map((f) => f.raceSessionId)).toEqual(['r2']); // r1 moved out
    const race = vm.resultRaces().find((r) => r.raceSessionId === 'r1')!;
    expect(race.entries.map((e) => [e.rank, e.swimmerName, e.isPersonalBest]))
      .toEqual([[1, 'Ahmed', true], [2, 'Sara', false]]); // sorted by time
  });

  it('partial results (one of two swimmers) still moves the race to Results', async () => {
    const results: RaceResultEntry[] = [{ raceSessionId: 'r1', swimmerId: 's1', timeMs: 24560, points: 0, isPersonalBest: true }];
    const { vm } = setup({ results });
    await vm.load('e1');
    expect(vm.finishedRaces().map((f) => f.raceSessionId)).toEqual(['r2']);
    expect(vm.resultRaces().find((r) => r.raceSessionId === 'r1')!.entries).toHaveLength(1);
  });

  it('saveResults skips blank/invalid inputs and posts only valid times', async () => {
    const saveRun = jest.fn().mockResolvedValue(ok([{ raceSessionId: 'r1', swimmerId: 's1', timeMs: 24560, points: 0, isPersonalBest: true }]));
    const { vm, notify } = setup({ saveRun });
    await vm.load('e1');
    vm.openResults('r1');
    vm.setTime('r1', 's1', '0:24.56');
    vm.setTime('r1', 's2', '   '); // blank → skipped
    await vm.saveResults('r1');
    expect(saveRun).toHaveBeenCalledWith({ eventId: 'e1', raceSessionId: 'r1', entries: [{ swimmerId: 's1', timeMs: 24560 }] });
    expect(notify.success).toHaveBeenCalled();
    expect(vm.isOpen('r1')).toBe(false);
  });

  it('saveResults is a no-op when there is nothing valid to save', async () => {
    const saveRun = jest.fn();
    const { vm } = setup({ saveRun });
    await vm.load('e1');
    vm.openResults('r2'); // race with no swimmers → no inputs
    await vm.saveResults('r2');
    expect(saveRun).not.toHaveBeenCalled();
  });

  it('saveResults surfaces an error toast on failure and keeps the card open', async () => {
    const saveRun = jest.fn().mockResolvedValue({ ok: false, error: new AppError('bad', 'http', 400) });
    const { vm, notify } = setup({ saveRun });
    await vm.load('e1');
    vm.openResults('r1');
    vm.setTime('r1', 's1', '0:24.56');
    await vm.saveResults('r1');
    expect(notify.error).toHaveBeenCalled();
    expect(vm.isOpen('r1')).toBe(true);
  });

  it('a non-manager cannot open or edit results', async () => {
    const saveRun = jest.fn();
    const { vm } = setup({ role: 'swimmer', saveRun });
    await vm.load('e1');
    vm.openResults('r1');
    expect(vm.isOpen('r1')).toBe(false);
    vm.setTime('r1', 's1', '0:24.56');
    expect(vm.timeInput('r1', 's1')).toBe('');
    await vm.saveResults('r1');
    expect(saveRun).not.toHaveBeenCalled();
  });

  it('sets error when a source fails to load', async () => {
    TestBed.resetTestingModule();
    const failing = jest.fn().mockResolvedValue({ ok: false, error: new AppError('x', 'http', 404) });
    TestBed.configureTestingModule({
      providers: [
        RaceResultsViewModel,
        { provide: LoadRaceScheduleUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(schedule)) } },
        { provide: LoadResultsUseCase, useValue: { run: failing } },
        { provide: SaveRaceResultsUseCase, useValue: { run: jest.fn() } },
        { provide: LoadStrokesUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(strokes)) } },
        { provide: LoadDistancesUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(distances)) } },
        { provide: ListSwimmersUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(roster)) } },
        { provide: LanguageStore, useValue: { lang: () => 'en' } },
        { provide: AuthSessionStore, useValue: { role: () => 'head_coach' } },
        { provide: NotificationService, useValue: { success: jest.fn(), error: jest.fn() } },
        { provide: TranslateService, useValue: { t: (k: string) => k } },
      ],
    });
    const vm2 = TestBed.inject(RaceResultsViewModel);
    await vm2.load('e1');
    expect(vm2.error()).toBe(true);
    expect(vm2.loaded()).toBe(false);
  });

  it('ensureLoaded only loads once per event', async () => {
    const { vm, schedRun } = setup();
    await vm.ensureLoaded('e1');
    await vm.ensureLoaded('e1');
    expect(schedRun).toHaveBeenCalledTimes(1);
  });
});
