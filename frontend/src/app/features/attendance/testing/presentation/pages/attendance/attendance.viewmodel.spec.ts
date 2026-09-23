import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { AttendanceEntryViewModel } from '@features/attendance/presentation/pages/attendance/attendance.viewmodel';
import { LoadAttendanceSessionUseCase } from '@features/attendance/domain/usecases/load-attendance-session.use-case';
import { SaveAttendanceSessionUseCase } from '@features/attendance/domain/usecases/save-attendance-session.use-case';
import { LoadAttendanceStatusesUseCase } from '@features/reference/domain/usecases/load-attendance-statuses.use-case';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';

const STATUSES = [
  { id: 'st1', code: 'present', nameEn: 'Present', nameAr: 'حاضر' },
  { id: 'st2', code: 'late',    nameEn: 'Late',    nameAr: 'متأخر' },
  { id: 'st3', code: 'absent',  nameEn: 'Absent',  nameAr: 'غائب' },
  { id: 'st4', code: 'excused', nameEn: 'Excused', nameAr: 'معذور' },
];

// Two swimmers: A already Present (has record); B has no record.
const SESSION = {
  date: '2026-09-23',
  rows: [
    { swimmerId: 'a', uid: 'SW-A', nameEn: 'Alice', nameAr: null, clubNameEn: null, clubNameAr: null,
      genderCode: 'female', statusId: 'st1', coachNote: 'ok', monthRatePct: 90, hasRecord: true },
    { swimmerId: 'b', uid: 'SW-B', nameEn: 'Bob', nameAr: null, clubNameEn: null, clubNameAr: null,
      genderCode: 'male', statusId: null, coachNote: null, monthRatePct: null, hasRecord: false },
  ],
};

function build(overrides: { load?: unknown; save?: unknown; role?: 'head_coach' | 'captain' | 'swimmer' | null } = {}) {
  const loadUc = { run: jest.fn().mockResolvedValue(overrides.load ?? { ok: true, data: SESSION }) };
  const saveUc = { run: jest.fn().mockResolvedValue(overrides.save ?? { ok: true, data: SESSION }) };
  const statusesUc = { run: jest.fn().mockResolvedValue({ ok: true, data: STATUSES }) };
  const notify = { success: jest.fn(), error: jest.fn() };
  const i18n = { t: (k: string) => k };
  const session = { role: signal<'head_coach' | 'captain' | 'swimmer' | null>(overrides.role ?? 'head_coach') };
  const language = { lang: signal<'en' | 'ar'>('en') };

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      AttendanceEntryViewModel,
      { provide: LoadAttendanceSessionUseCase, useValue: loadUc },
      { provide: SaveAttendanceSessionUseCase, useValue: saveUc },
      { provide: LoadAttendanceStatusesUseCase, useValue: statusesUc },
      { provide: AuthSessionStore, useValue: session },
      { provide: NotificationService, useValue: notify },
      { provide: TranslateService, useValue: i18n },
      { provide: LanguageStore, useValue: language },
    ],
  });
  return { vm: TestBed.inject(AttendanceEntryViewModel), loadUc, saveUc, statusesUc, notify, session };
}

describe('AttendanceEntryViewModel', () => {
  it('loads a session and defaults unrecorded rows to Present', async () => {
    const { vm, loadUc } = build();
    await vm.load('2026-09-23');

    expect(loadUc.run).toHaveBeenCalledWith('2026-09-23');
    const rows = vm.rows();
    expect(rows).toHaveLength(2);
    expect(rows.find((r) => r.swimmerId === 'a')!.statusId).toBe('st1');
    // B had null → defaulted to Present.
    expect(rows.find((r) => r.swimmerId === 'b')!.statusId).toBe('st1');
  });

  it('is dirty right after load because B has no saved record (fresh default)', async () => {
    const { vm } = build();
    await vm.load('2026-09-23');
    expect(vm.dirty()).toBe(true);
  });

  it('markAllPresent sets every row to the Present status', async () => {
    const { vm } = build();
    await vm.load('2026-09-23');
    vm.setStatus('a', 'st3'); // Absent
    vm.markAllPresent();
    expect(vm.rows().every((r) => r.statusId === 'st1')).toBe(true);
  });

  it('presentCount counts present and late', async () => {
    const { vm } = build();
    await vm.load('2026-09-23');
    vm.setStatus('a', 'st2'); // Late
    vm.setStatus('b', 'st3'); // Absent
    expect(vm.presentCount()).toBe(1); // only the late one counts as "in"
    expect(vm.total()).toBe(2);
  });

  it('save posts every row and refreshes from the response', async () => {
    const { vm, saveUc } = build();
    await vm.load('2026-09-23');
    await vm.save();

    expect(saveUc.run).toHaveBeenCalledTimes(1);
    const payload = saveUc.run.mock.calls[0][0];
    expect(payload.date).toBe('2026-09-23');
    expect(payload.entries).toHaveLength(2); // all rows sent, incl. defaulted B
    expect(payload.entries.every((e: { statusId: string }) => typeof e.statusId === 'string')).toBe(true);
  });

  it('canSave is false for a non-coach role', async () => {
    const { vm } = build({ role: 'swimmer' });
    await vm.load('2026-09-23');
    expect(vm.canSave()).toBe(false);
  });

  it('rateBand maps thresholds', () => {
    const { vm } = build();
    expect(vm.rateBand(null)).toBe('muted');
    expect(vm.rateBand(65)).toBe('red');
    expect(vm.rateBand(80)).toBe('amber');
    expect(vm.rateBand(90)).toBe('green');
  });
});
