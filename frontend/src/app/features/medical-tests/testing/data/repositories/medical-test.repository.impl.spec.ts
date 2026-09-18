import { TestBed } from '@angular/core/testing';
import { MedicalTestRepositoryImpl } from '@features/medical-tests/data/repositories/medical-test.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('MedicalTestRepositoryImpl', () => {
  const http = { get: jest.fn(), post: jest.fn(), delete: jest.fn() } as unknown as HttpClientService;
  let repo: MedicalTestRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [MedicalTestRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(MedicalTestRepositoryImpl);
  });

  it('list GETs /api/medical-tests', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.list();
    expect(http.get).toHaveBeenCalledWith('/api/medical-tests');
  });

  it('create POSTs /api/medical-tests with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: {} });
    const rq = { nameEn: 'Vit D', nameAr: 'د', unit: 'ng/mL', lowerBound: 30, upperBound: 100 };
    await repo.create(rq);
    expect(http.post).toHaveBeenCalledWith('/api/medical-tests', { body: rq });
  });

  it('delete DELETEs /api/medical-tests/{id}', async () => {
    (http.delete as jest.Mock).mockResolvedValue({ data: 'abc' });
    await repo.delete('abc');
    expect(http.delete).toHaveBeenCalledWith('/api/medical-tests/abc');
  });
});
