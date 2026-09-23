import { TestBed } from '@angular/core/testing';
import { SaveAttendanceSessionUseCase } from '@features/attendance/domain/usecases/save-attendance-session.use-case';
import { ATTENDANCE_ENTRY_REPOSITORY } from '@features/attendance/domain/repositories/attendance-entry.repository';

const SESSION = { date: '2026-09-23', rows: [] };

describe('SaveAttendanceSessionUseCase', () => {
  it('posts the payload and maps the refreshed session', async () => {
    const repo = { getSession: jest.fn(), saveSession: jest.fn().mockResolvedValue({ data: SESSION }) };
    TestBed.configureTestingModule({ providers: [{ provide: ATTENDANCE_ENTRY_REPOSITORY, useValue: repo }] });
    const uc = TestBed.inject(SaveAttendanceSessionUseCase);

    const rq = { date: '2026-09-23', entries: [{ swimmerId: 's1', statusId: 'st1', coachNote: null }] };
    const r = await uc.run(rq);
    expect(r.ok).toBe(true);
    expect(repo.saveSession).toHaveBeenCalledWith(rq);
  });
});
