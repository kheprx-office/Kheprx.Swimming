import { toAttendanceSession } from '@features/attendance/data/dto/attendance-session.mapper';
import { isAttendanceSessionDtoRsValid } from '@features/attendance/data/dto/attendance-session.dto';

describe('attendance-session mapper', () => {
  const dto = {
    date: '2026-09-23',
    rows: [{
      swimmerId: 's1', uid: 'SW-1', nameEn: 'Alice', nameAr: null,
      clubNameEn: 'Oasis', clubNameAr: null, genderCode: 'female',
      statusId: 'st1', coachNote: 'great', monthRatePct: 67, hasRecord: true,
    }],
  };

  it('accepts a valid dto and maps date + rows', () => {
    expect(isAttendanceSessionDtoRsValid(dto)).toBe(true);
    const session = toAttendanceSession(dto);
    expect(session.date).toBe('2026-09-23');
    expect(session.rows).toHaveLength(1);
    expect(session.rows[0].swimmerId).toBe('s1');
    expect(session.rows[0].hasRecord).toBe(true);
    expect(session.rows[0].monthRatePct).toBe(67);
  });

  it('rejects a malformed dto', () => {
    expect(isAttendanceSessionDtoRsValid({ date: 5, rows: 'x' })).toBe(false);
  });
});
