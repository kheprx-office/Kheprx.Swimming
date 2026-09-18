import { TestBed } from '@angular/core/testing';
import { ObservationRepositoryImpl } from '@features/observations/data/repositories/observation.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('ObservationRepositoryImpl', () => {
  const http = { post: jest.fn() } as unknown as HttpClientService;
  let repo: ObservationRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [ObservationRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(ObservationRepositoryImpl);
  });

  it('create POSTs /api/observations with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: {} });
    const rq = { swimmerId: 's1', categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe' };
    await repo.create(rq);
    expect(http.post).toHaveBeenCalledWith('/api/observations', { body: rq });
  });
});
