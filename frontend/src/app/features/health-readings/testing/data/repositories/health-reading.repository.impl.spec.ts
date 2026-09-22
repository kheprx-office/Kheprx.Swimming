import { TestBed } from '@angular/core/testing';
import { HealthReadingRepositoryImpl } from '@features/health-readings/data/repositories/health-reading.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('HealthReadingRepositoryImpl', () => {
  const http = { post: jest.fn(), get: jest.fn(), put: jest.fn(), delete: jest.fn() } as unknown as HttpClientService;
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

  it('list GETs /api/health-readings filtered by swimmerId', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.list('s1');
    expect(http.get).toHaveBeenCalledWith('/api/health-readings?swimmerId=s1');
  });

  it('update PUTs /api/health-readings/{id} with the value body', async () => {
    (http.put as jest.Mock).mockResolvedValue({ data: {} });
    await repo.update('r1', { value: 12.3 });
    expect(http.put).toHaveBeenCalledWith('/api/health-readings/r1', { body: { value: 12.3 } });
  });

  it('remove DELETEs /api/health-readings/{id}', async () => {
    (http.delete as jest.Mock).mockResolvedValue({ data: null });
    await repo.remove('r1');
    expect(http.delete).toHaveBeenCalledWith('/api/health-readings/r1');
  });
});
