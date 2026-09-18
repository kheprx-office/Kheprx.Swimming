import { TestBed } from '@angular/core/testing';
import { SwimmerRepositoryImpl } from '@features/swimmers/data/repositories/swimmer.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('SwimmerRepositoryImpl', () => {
  const http = { get: jest.fn(), post: jest.fn() } as unknown as HttpClientService;
  let repo: SwimmerRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [SwimmerRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(SwimmerRepositoryImpl);
  });

  it('getCount GETs the swimmer count envelope', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: { count: 24 } });
    await expect(repo.getCount()).resolves.toEqual({ data: { count: 24 } });
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/count');
  });

  it('create POSTs /api/swimmers with the body', async () => {
    (http.post as jest.Mock) = jest.fn().mockResolvedValue({ data: {} });
    const rq = { nameEn: 'Mona', username: 'mona', trainingClubId: 'c1', genderId: 'g1', dob: '2010-05-01', bloodTypeId: 'b1', strokeIds: ['s1'] };
    await repo.create(rq as never);
    expect((http.post as jest.Mock)).toHaveBeenCalledWith('/api/swimmers', { body: rq });
  });

  it('list GETs /api/swimmers with the search param', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.list('ali');
    expect(http.get).toHaveBeenCalledWith('/api/swimmers', { params: { search: 'ali' } });
  });

  it('list GETs /api/swimmers with no options when search is empty', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.list('');
    expect(http.get).toHaveBeenCalledWith('/api/swimmers', undefined);
  });
});
