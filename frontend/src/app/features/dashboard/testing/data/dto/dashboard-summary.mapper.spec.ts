import { toDashboardSummary } from '@features/dashboard/data/dto/dashboard-summary.mapper';
import { DashboardSummaryDtoRs } from '@features/dashboard/data/dto/dashboard-summary.dto';

describe('toDashboardSummary', () => {
  it('maps all fields and defaults missing nameAr to null', () => {
    const dto: DashboardSummaryDtoRs = {
      swimmerCount: 452,
      newThisMonth: 12,
      monthAttendanceRatePct: 88,
      last7Days: [{ date: '2026-09-28', present: 5, absent: 1 }],
      strokeSplit: [{ strokeId: 's1', code: 'free', nameEn: 'Freestyle', count: 9 }],
    };
    const model = toDashboardSummary(dto);
    expect(model.swimmerCount).toBe(452);
    expect(model.last7Days[0].present).toBe(5);
    expect(model.strokeSplit[0].nameAr).toBeNull();
  });

  it('preserves a null attendance rate', () => {
    const dto: DashboardSummaryDtoRs = {
      swimmerCount: 0, newThisMonth: 0, monthAttendanceRatePct: null, last7Days: [], strokeSplit: [],
    };
    expect(toDashboardSummary(dto).monthAttendanceRatePct).toBeNull();
  });
});
