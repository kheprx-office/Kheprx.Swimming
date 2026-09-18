import { TestBed } from '@angular/core/testing';
import { ReferenceRepositoryImpl } from '@features/reference/data/repositories/reference.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('ReferenceRepositoryImpl', () => {
  const http = { get: jest.fn() } as unknown as HttpClientService;
  let repo: ReferenceRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [ReferenceRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(ReferenceRepositoryImpl);
  });

  it('getClubs GETs /api/reference/clubs', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.getClubs();
    expect(http.get).toHaveBeenCalledWith('/api/reference/clubs');
  });

  it('getBloodTypes GETs /api/reference/blood-types', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.getBloodTypes();
    expect(http.get).toHaveBeenCalledWith('/api/reference/blood-types');
  });

  it('getStrokes GETs /api/reference/strokes', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.getStrokes();
    expect(http.get).toHaveBeenCalledWith('/api/reference/strokes');
  });

  it('getGenders GETs /api/reference/genders', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.getGenders();
    expect(http.get).toHaveBeenCalledWith('/api/reference/genders');
  });
});
