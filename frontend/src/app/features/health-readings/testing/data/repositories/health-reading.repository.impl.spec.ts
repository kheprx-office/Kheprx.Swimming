import { TestBed } from '@angular/core/testing';
import { HealthReadingRepositoryImpl } from '@features/health-readings/data/repositories/health-reading.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('HealthReadingRepositoryImpl', () => {
  const http = { post: jest.fn() } as unknown as HttpClientService;
  let repo: HealthReadingRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [HealthReadingRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(HealthReadingRepositoryImpl);
  });

  it('create POSTs /api/health-readings with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: {} });
    const rq = { swimmerId: 's1', medicalTestId: 't1', value: 95 };
    await repo.create(rq);
    expect(http.post).toHaveBeenCalledWith('/api/health-readings', { body: rq });
  });
});
