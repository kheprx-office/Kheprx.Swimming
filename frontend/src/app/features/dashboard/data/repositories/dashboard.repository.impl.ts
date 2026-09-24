// dashboard.repository.impl.ts — dashboard repository. Returns the API DTO envelope.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IDashboardRepository } from '@features/dashboard/domain/repositories/dashboard.repository';
import { DashboardSummaryItemDtoRs } from '@features/dashboard/data/dto/dashboard-summary.dto';

@Injectable({ providedIn: 'root' })
export class DashboardRepositoryImpl implements IDashboardRepository {
  private readonly http = inject(HttpClientService);

  getSummary(): Promise<DashboardSummaryItemDtoRs> {
    return this.http.get<DashboardSummaryItemDtoRs>('/api/dashboard/summary');
  }
}
