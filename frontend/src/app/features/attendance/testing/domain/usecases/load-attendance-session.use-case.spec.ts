import { TestBed } from '@angular/core/testing';
import { LoadAttendanceSessionUseCase } from '@features/attendance/domain/usecases/load-attendance-session.use-case';
import { ATTENDANCE_ENTRY_REPOSITORY } from '@features/attendance/domain/repositories/attendance-entry.repository';

const SESSION = {
  date: '2026-09-23',
  rows: [{ swimmerId: 's1', uid: 'SW-1', nameEn: 'Alice', nameAr: null, clubNameEn: null, clubNameAr: null,
           genderCode: 'female', statusId: null, coachNote: null, monthRatePct: null, hasRecord: false }],
};

describe('LoadAttendanceSessionUseCase', () => {
  it('maps a valid response to a domain session', async () => {
    const repo = { getSession: jest.fn().mockResolvedValue({ data: SESSION }), saveSession: jest.fn() };
    TestBed.configureTestingModule({ providers: [{ provide: ATTENDANCE_ENTRY_REPOSITORY, useValue: repo }] });
    const uc = TestBed.inject(LoadAttendanceSessionUseCase);

    const r = await uc.run('2026-09-23');
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data.rows).toHaveLength(1); expect(r.data.rows[0].hasRecord).toBe(false); }
    expect(repo.getSession).toHaveBeenCalledWith('2026-09-23');
  });

  it('fails on an invalid response', async () => {
    const repo = { getSession: jest.fn().mockResolvedValue({ data: { date: 1 } }), saveSession: jest.fn() };
    TestBed.configureTestingModule({ providers: [{ provide: ATTENDANCE_ENTRY_REPOSITORY, useValue: repo }] });
    const uc = TestBed.inject(LoadAttendanceSessionUseCase);
    const r = await uc.run('2026-09-23');
    expect(r.ok).toBe(false);
  });
});
