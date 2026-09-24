import { TestBed } from '@angular/core/testing';
import { ChampionshipsRepositoryImpl } from '@features/championships/data/repositories/championships.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('ChampionshipsRepositoryImpl (detail + enrollment)', () => {
  const http = { get: jest.fn(), post: jest.fn(), put: jest.fn() } as unknown as HttpClientService;
  let repo: ChampionshipsRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [ChampionshipsRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(ChampionshipsRepositoryImpl);
  });

  it('getChampionship GETs /api/championships/{id}', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: null });
    await repo.getChampionship('e1');
    expect(http.get).toHaveBeenCalledWith('/api/championships/e1');
  });

  it('getEnrollments GETs the enrollments endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.getEnrollments('e1');
    expect(http.get).toHaveBeenCalledWith('/api/championships/e1/enrollments');
  });

  it('setEnrollments PUTs the payload as the body', async () => {
    (http.put as jest.Mock).mockResolvedValue({ data: [] });
    const rq = { swimmerIds: ['s1', 's2'] };
    await repo.setEnrollments('e1', rq);
    expect(http.put).toHaveBeenCalledWith('/api/championships/e1/enrollments', { body: rq });
  });

  it('getResults GETs the results endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: { results: [] } });
    await repo.getResults('e1');
    expect(http.get).toHaveBeenCalledWith('/api/championships/e1/results');
  });

  it('setRaceResults PUTs to the per-race results endpoint with the body', async () => {
    (http.put as jest.Mock).mockResolvedValue({ data: { results: [] } });
    const rq = { entries: [{ swimmerId: 's1', timeMs: 24560 }] };
    await repo.setRaceResults('e1', 'race1', rq);
    expect(http.put).toHaveBeenCalledWith('/api/championships/e1/races/race1/results', { body: rq });
  });

  it('getSwimmerChampionshipHistory GETs the swimmer history endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.getSwimmerChampionshipHistory('sw1');
    expect(http.get).toHaveBeenCalledWith('/api/championships/swimmer/sw1/history');
  });
});
