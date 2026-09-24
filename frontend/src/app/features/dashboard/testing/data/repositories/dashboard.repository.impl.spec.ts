import { TestBed } from '@angular/core/testing';
import { DashboardRepositoryImpl } from '@features/dashboard/data/repositories/dashboard.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('DashboardRepositoryImpl', () => {
  const http = { get: jest.fn() } as unknown as HttpClientService;
  let repo: DashboardRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [DashboardRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(DashboardRepositoryImpl);
  });

  it('getSummary GETs the dashboard summary endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: null });
    await repo.getSummary();
    expect(http.get).toHaveBeenCalledWith('/api/dashboard/summary');
  });
});
