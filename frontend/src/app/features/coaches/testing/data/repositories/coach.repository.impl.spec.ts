import { TestBed } from '@angular/core/testing';
import { CoachRepositoryImpl } from '@features/coaches/data/repositories/coach.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('CoachRepositoryImpl', () => {
  const http = { post: jest.fn() } as unknown as HttpClientService;
  let repo: CoachRepositoryImpl;
  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({ providers: [CoachRepositoryImpl, { provide: HttpClientService, useValue: http }] });
    repo = TestBed.inject(CoachRepositoryImpl);
  });
  it('create POSTs /api/coaches with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: {} });
    const rq = { role: 'captain', nameEn: 'Dave', username: 'dave', email: 'd@o.com', nationalId: '29001011234567', genderId: 'g1', dob: '1990-01-01', phone: '01000000000' };
    await repo.create(rq as never);
    expect(http.post).toHaveBeenCalledWith('/api/coaches', { body: rq });
  });
});
