import { TestBed } from '@angular/core/testing';
import { ok } from '@core/domain/result/result';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { CompetitionDaysViewModel } from '@features/championships/presentation/pages/championship-detail/competition-days.viewmodel';
import { LoadScheduleUseCase } from '@features/championships/domain/usecases/load-schedule.use-case';
import { SaveScheduleUseCase } from '@features/championships/domain/usecases/save-schedule.use-case';
import { LoadStrokesUseCase, LoadDistancesUseCase } from '@features/reference';
import { LoadEnrollmentsUseCase } from '@features/championships/domain/usecases/load-enrollments.use-case';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { ScheduleDayData } from '@features/championships/domain/model/competition-schedule';
import { AppError } from '@core/domain/errors/app-error';

const strokes = [{ id: 'st1', code: 'freestyle', nameEn: 'Freestyle', nameAr: null }];
const distances = [{ id: 'ds1', code: '50m', nameEn: '50m', nameAr: null }];
const roster = [
  { id: 's1', uid: 'U1', nameEn: 'Ahmed', nameAr: 'أحمد', clubNameEn: 'Oasis', clubNameAr: null, gender: 'male', age: 18 },
  { id: 's2', uid: 'U2', nameEn: 'Sara', nameAr: 'سارة', clubNameEn: 'Oasis', clubNameAr: null, gender: 'female', age: 16 },
];
const seededDay: ScheduleDayData = {
  labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
  races: [{ strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1'] }],
};

interface Opts { role?: string; schedRun?: jest.Mock; saveRun?: jest.Mock; enrRun?: jest.Mock; }

function setup(opts: Opts = {}) {
  const schedRun = opts.schedRun ?? jest.fn().mockResolvedValue(ok([seededDay]));
  const saveRun = opts.saveRun ?? jest.fn().mockResolvedValue(ok([seededDay]));
  const enrRun = opts.enrRun ?? jest.fn().mockResolvedValue(ok(['s1', 's2']));
  const notify = { success: jest.fn(), error: jest.fn() };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      CompetitionDaysViewModel,
      { provide: LoadScheduleUseCase, useValue: { run: schedRun } },
      { provide: SaveScheduleUseCase, useValue: { run: saveRun } },
      { provide: LoadStrokesUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(strokes)) } },
      { provide: LoadDistancesUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(distances)) } },
      { provide: LoadEnrollmentsUseCase, useValue: { run: enrRun } },
      { provide: ListSwimmersUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(roster)) } },
      { provide: LanguageStore, useValue: { lang: () => 'en' } },
      { provide: AuthSessionStore, useValue: { role: () => opts.role ?? 'head_coach' } },
      { provide: NotificationService, useValue: notify },
      { provide: TranslateService, useValue: { t: (k: string) => k } },
    ],
  });
  return { vm: TestBed.inject(CompetitionDaysViewModel), schedRun, saveRun, enrRun, notify };
}

describe('CompetitionDaysViewModel', () => {
  it('loads the schedule, lookups, roster and enrolled set', async () => {
    const { vm } = setup();
    await vm.load('e1');
    expect(vm.loaded()).toBe(true);
    expect(vm.dayCount()).toBe(1);
    expect(vm.raceCount()).toBe(1);
    expect(vm.entryCount()).toBe(1);
    expect(vm.enrolledSwimmers().map((s) => s.id)).toEqual(['s1', 's2']);
    expect(vm.dirty()).toBe(false);
  });

  it('ensureLoaded only loads once per event', async () => {
    const { vm, schedRun } = setup();
    await vm.ensureLoaded('e1');
    await vm.ensureLoaded('e1');
    expect(schedRun).toHaveBeenCalledTimes(1);
  });

  it('re-opening the tab refreshes the enrolled set but keeps unsaved schedule edits', async () => {
    const { vm, schedRun, enrRun } = setup();
    await vm.ensureLoaded('e1');
    expect(vm.enrolledSwimmers().map((s) => s.id)).toEqual(['s1', 's2']);

    // Coach makes an unsaved schedule edit, then enrollment changes on the Enrollment tab.
    vm.addDay();
    const dayCountBefore = vm.dayCount();
    expect(vm.dirty()).toBe(true);
    enrRun.mockResolvedValue(ok(['s1'])); // s2 un-enrolled elsewhere

    await vm.ensureLoaded('e1'); // re-open Competition Days

    expect(vm.enrolledSwimmers().map((s) => s.id)).toEqual(['s1']); // chip list refreshed
    expect(schedRun).toHaveBeenCalledTimes(1); // schedule NOT reloaded
    expect(vm.dayCount()).toBe(dayCountBefore); // unsaved day preserved
    expect(vm.dirty()).toBe(true); // edits still unsaved
  });

  it('sets error when the schedule fails to load', async () => {
    const schedRun = jest.fn().mockResolvedValue({ ok: false, error: new AppError('x', 'http', 404) });
    const { vm } = setup({ schedRun });
    await vm.load('e1');
    expect(vm.error()).toBe(true);
    expect(vm.loaded()).toBe(false);
  });

  it('addDay / addRace / toggleSwimmer mutate the tree and drive totals + dirty', async () => {
    const { vm } = setup();
    await vm.load('e1');
    vm.addDay();
    expect(vm.dayCount()).toBe(2);
    expect(vm.dirty()).toBe(true);
    const newDay = vm.days()[1];
    vm.addRace(newDay.key);
    expect(vm.raceCount()).toBe(2);
    const race = vm.days()[1].races[0];
    vm.toggleSwimmer(newDay.key, race.key, 's2');
    expect(vm.isAssigned(newDay.key, race.key, 's2')).toBe(true);
    expect(vm.entryCount()).toBe(2);
  });

  it('addDay fills the next free in-range date and disables once every date is used', async () => {
    const { vm } = setup();
    // 2-day event; the seeded schedule already has a day on 2023-11-15.
    await vm.ensureLoaded('e1', '2023-11-15', '2023-11-16');
    expect(vm.days().map((d) => d.dayDate)).toEqual(['2023-11-15']);
    expect(vm.canAddDay()).toBe(true);

    vm.addDay(); // fills the next free date (2023-11-16)
    expect(vm.days().map((d) => d.dayDate)).toEqual(['2023-11-15', '2023-11-16']);
    expect(vm.canAddDay()).toBe(false); // both in-range dates used

    vm.addDay(); // no-op — range full
    expect(vm.dayCount()).toBe(2);
  });

  it('canAddDay is true when the range is unknown (no dates supplied)', async () => {
    const { vm } = setup();
    await vm.load('e1'); // no start/end → unknown range, don't block
    expect(vm.canAddDay()).toBe(true);
  });

  it('canAddDay is false once the day count fills the range, even for out-of-range dates', async () => {
    // 3-date range (23–25 Sept), but the saved schedule holds 3 days on OUT-OF-RANGE dates.
    const schedRun = jest.fn().mockResolvedValue(ok([
      { labelEn: 'Day 1', labelAr: null, dayDate: '2026-09-21', races: [] },
      { labelEn: 'Day 2', labelAr: null, dayDate: '2026-09-22', races: [] },
      { labelEn: 'Day 3', labelAr: null, dayDate: '2026-09-20', races: [] },
    ]));
    const { vm } = setup({ schedRun });
    await vm.ensureLoaded('e1', '2026-09-23', '2026-09-25');
    expect(vm.dayCount()).toBe(3);
    expect(vm.canAddDay()).toBe(false); // 3 days already == 3 dates in range → at capacity
    vm.addDay();
    expect(vm.dayCount()).toBe(3); // no-op — range full
  });

  it('dayDateLabel formats a date read-only and blanks an empty date', async () => {
    const { vm } = setup();
    await vm.load('e1');
    const label = vm.dayDateLabel('2026-09-24');
    expect(label).toContain('2026');
    expect(label).not.toBe('2026-09-24'); // formatted, not the raw ISO
    expect(vm.dayDateLabel('')).toBe('');
  });

  it('removeDay / removeRace shrink the tree', async () => {
    const { vm } = setup();
    await vm.load('e1');
    const day = vm.days()[0];
    vm.removeRace(day.key, day.races[0].key);
    expect(vm.raceCount()).toBe(0);
    vm.removeDay(day.key);
    expect(vm.dayCount()).toBe(0);
  });

  it('updateRace changes distance/stroke/time', async () => {
    const { vm } = setup();
    await vm.load('e1');
    const day = vm.days()[0];
    vm.updateRace(day.key, day.races[0].key, { scheduledTime: '10:30' });
    expect(vm.days()[0].races[0].scheduledTime).toBe('10:30');
  });

  it('edits are no-ops for a non-manager', async () => {
    const { vm } = setup({ role: 'swimmer' });
    await vm.load('e1');
    vm.addDay();
    expect(vm.dayCount()).toBe(1);
    expect(vm.dirty()).toBe(false);
  });

  it('save persists, toasts success, rebuilds baseline and clears dirty', async () => {
    const { vm, saveRun, notify } = setup();
    await vm.load('e1');
    vm.addDay();
    expect(vm.dirty()).toBe(true);
    const keyBefore = vm.days()[0].key;
    const raceKeyBefore = vm.days()[0].races[0].key;
    saveRun.mockResolvedValue(ok(vm.days().map((d) => ({ labelEn: d.labelEn, labelAr: d.labelAr, dayDate: d.dayDate, races: d.races.map((r) => ({ strokeId: r.strokeId, distanceId: r.distanceId, scheduledTime: r.scheduledTime, swimmerIds: r.swimmerIds })) }))));
    await vm.save();
    expect(saveRun).toHaveBeenCalled();
    expect(notify.success).toHaveBeenCalled();
    expect(vm.dirty()).toBe(false);
    // keys must be preserved across save (no DOM churn)
    expect(vm.days()[0].key).toBe(keyBefore);
    expect(vm.days()[0].races[0].key).toBe(raceKeyBefore);
  });

  it('save surfaces an error toast and keeps the working tree on failure', async () => {
    const saveRun = jest.fn().mockResolvedValue({ ok: false, error: new AppError('bad', 'http', 400) });
    const { vm, notify } = setup({ saveRun });
    await vm.load('e1');
    vm.addDay();
    await vm.save();
    expect(notify.error).toHaveBeenCalled();
    expect(vm.dayCount()).toBe(2);
    expect(vm.dirty()).toBe(true);
  });

  it('updateDay patches label and date and marks dirty', async () => {
    const { vm } = setup();
    await vm.load('e1');
    const day = vm.days()[0];
    vm.updateDay(day.key, { labelEn: 'Renamed', dayDate: '2023-11-20' });
    expect(vm.days()[0].labelEn).toBe('Renamed');
    expect(vm.days()[0].dayDate).toBe('2023-11-20');
    expect(vm.dirty()).toBe(true);
  });

  it('save is a no-op when not dirty (captain with clean tree)', async () => {
    const saveRun = jest.fn();
    const { vm } = setup({ role: 'captain', saveRun });
    await vm.load('e1');            // clean — dirty() === false
    await vm.save();
    expect(saveRun).not.toHaveBeenCalled();
  });

  it('save is a no-op for a non-manager (swimmer) regardless of state', async () => {
    const saveRun = jest.fn();
    const { vm } = setup({ role: 'swimmer', saveRun });
    await vm.load('e1');
    // a swimmer cannot mutate the tree, but even calling save() directly must be a no-op
    await vm.save();
    expect(saveRun).not.toHaveBeenCalled();
  });
});
