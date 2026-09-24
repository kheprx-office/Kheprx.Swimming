import { InjectionToken } from '@angular/core';
import { DashboardSummaryItemDtoRs } from '@features/dashboard/data/dto/dashboard-summary.dto';

export interface IDashboardRepository {
  getSummary(): Promise<DashboardSummaryItemDtoRs>;
}

export const DASHBOARD_REPOSITORY = new InjectionToken<IDashboardRepository>('DASHBOARD_REPOSITORY');
