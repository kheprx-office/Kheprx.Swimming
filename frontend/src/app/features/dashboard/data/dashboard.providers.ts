import { Provider } from '@angular/core';
import { DASHBOARD_REPOSITORY } from '@features/dashboard/domain/repositories/dashboard.repository';
import { DashboardRepositoryImpl } from '@features/dashboard/data/repositories/dashboard.repository.impl';

export const DASHBOARD_PROVIDERS: Provider[] = [
  { provide: DASHBOARD_REPOSITORY, useClass: DashboardRepositoryImpl },
];
