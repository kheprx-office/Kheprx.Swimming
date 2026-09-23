export interface AttendanceRecord {
  id: string;
  sessionDate: string;      // 'YYYY-MM-DD'
  statusId: string;
  coachNoteEn: string | null;
  coachNoteAr: string | null;
  recordedByNameEn: string;
  recordedByNameAr: string | null;
}
