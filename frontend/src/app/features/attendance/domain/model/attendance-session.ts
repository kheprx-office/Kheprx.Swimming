export interface SessionRow {
  swimmerId: string;
  uid: string;
  nameEn: string;
  nameAr: string | null;
  clubNameEn: string | null;
  clubNameAr: string | null;
  genderCode: string;
  statusId: string | null;   // null = no record yet for this date
  coachNote: string | null;
  monthRatePct: number | null;
  hasRecord: boolean;
}

export interface AttendanceSession {
  date: string;              // 'YYYY-MM-DD'
  rows: SessionRow[];
}
