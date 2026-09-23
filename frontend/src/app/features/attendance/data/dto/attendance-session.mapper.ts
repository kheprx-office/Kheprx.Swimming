import { AttendanceSessionDtoRs, SwimmerSessionRowDtoRs } from '@features/attendance/data/dto/attendance-session.dto';
import { AttendanceSession, SessionRow } from '@features/attendance/domain/model/attendance-session';

function toRow(d: SwimmerSessionRowDtoRs): SessionRow {
  return {
    swimmerId: d.swimmerId,
    uid: d.uid,
    nameEn: d.nameEn,
    nameAr: d.nameAr,
    clubNameEn: d.clubNameEn,
    clubNameAr: d.clubNameAr,
    genderCode: d.genderCode,
    statusId: d.statusId,
    coachNote: d.coachNote,
    monthRatePct: d.monthRatePct,
    hasRecord: d.hasRecord,
  };
}

export function toAttendanceSession(d: AttendanceSessionDtoRs): AttendanceSession {
  return { date: d.date, rows: d.rows.map(toRow) };
}
