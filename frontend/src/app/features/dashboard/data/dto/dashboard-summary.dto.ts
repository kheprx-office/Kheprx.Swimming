// dashboard-summary.dto.ts — dashboard summary response DTO (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface DailyAttendanceDtoRs {
  date: string;
  present: number;
  absent: number;
}

export interface StrokeSplitItemDtoRs {
  strokeId: string;
  code: string;
  nameEn: string;
  nameAr?: string | null;
  count: number;
}

export interface DashboardSummaryDtoRs {
  swimmerCount: number;
  newThisMonth: number;
  monthAttendanceRatePct: number | null;
  last7Days: DailyAttendanceDtoRs[];
  strokeSplit: StrokeSplitItemDtoRs[];
}

export interface DashboardSummaryItemDtoRs extends BaseResponseRs<DashboardSummaryDtoRs> {}

export function isDashboardSummaryDtoRsValid(dto: unknown): dto is DashboardSummaryDtoRs {
  const d = dto as DashboardSummaryDtoRs;
  return (
    !!d &&
    typeof d.swimmerCount === 'number' &&
    typeof d.newThisMonth === 'number' &&
    (d.monthAttendanceRatePct === null || typeof d.monthAttendanceRatePct === 'number') &&
    Array.isArray(d.last7Days) &&
    Array.isArray(d.strokeSplit)
  );
}
