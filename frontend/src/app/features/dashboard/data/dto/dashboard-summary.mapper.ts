import { DashboardSummaryDtoRs } from '@features/dashboard/data/dto/dashboard-summary.dto';
import { DashboardSummary } from '@features/dashboard/domain/model/dashboard-summary';

export function toDashboardSummary(d: DashboardSummaryDtoRs): DashboardSummary {
  return {
    swimmerCount: d.swimmerCount,
    newThisMonth: d.newThisMonth,
    monthAttendanceRatePct: d.monthAttendanceRatePct,
    last7Days: d.last7Days.map((x) => ({ date: x.date, present: x.present, absent: x.absent })),
    strokeSplit: d.strokeSplit.map((x) => ({
      strokeId: x.strokeId,
      code: x.code,
      nameEn: x.nameEn,
      nameAr: x.nameAr ?? null,
      count: x.count,
    })),
  };
}
