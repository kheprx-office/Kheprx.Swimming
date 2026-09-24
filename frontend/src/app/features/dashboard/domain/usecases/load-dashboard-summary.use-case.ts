import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { DASHBOARD_REPOSITORY } from '@features/dashboard/domain/repositories/dashboard.repository';
import { isDashboardSummaryDtoRsValid } from '@features/dashboard/data/dto/dashboard-summary.dto';
import { toDashboardSummary } from '@features/dashboard/data/dto/dashboard-summary.mapper';
import { DashboardSummary } from '@features/dashboard/domain/model/dashboard-summary';

@Injectable({ providedIn: 'root' })
export class LoadDashboardSummaryUseCase extends UseCase<void, DashboardSummary> {
  private readonly repo = inject(DASHBOARD_REPOSITORY);
  constructor() { super('LoadDashboardSummary'); }
  protected async execute(): Promise<DashboardSummary> {
    const res = await this.repo.getSummary();
    if (!isDashboardSummaryDtoRsValid(res.data)) {
      throw new AppError('Invalid dashboard summary received', 'validation');
    }
    return toDashboardSummary(res.data);
  }
}
