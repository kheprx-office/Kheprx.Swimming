export interface DailyAttendance {
  date: string; // 'YYYY-MM-DD'
  present: number;
  absent: number;
}

export interface StrokeSplitItem {
  strokeId: string;
  code: string;
  nameEn: string;
  nameAr: string | null;
  count: number;
}

export interface DashboardSummary {
  swimmerCount: number;
  newThisMonth: number;
  monthAttendanceRatePct: number | null;
  last7Days: DailyAttendance[];
  strokeSplit: StrokeSplitItem[];
}
