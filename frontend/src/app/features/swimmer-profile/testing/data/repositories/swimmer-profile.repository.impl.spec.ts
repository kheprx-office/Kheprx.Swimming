import { TestBed } from '@angular/core/testing';
import { SwimmerProfileRepositoryImpl } from '@features/swimmer-profile/data/repositories/swimmer-profile.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('SwimmerProfileRepositoryImpl', () => {
  const http = { get: jest.fn(), put: jest.fn(), post: jest.fn(), delete: jest.fn() } as unknown as HttpClientService;
  let repo: SwimmerProfileRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({ providers: [SwimmerProfileRepositoryImpl, { provide: HttpClientService, useValue: http }] });
    repo = TestBed.inject(SwimmerProfileRepositoryImpl);
  });

  it('getProfile GETs /api/swimmers/{id}', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: {} });
    await repo.getProfile('s1');
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/s1');
  });

  it('updateIdentity PUTs /api/swimmers/{id}/identity with the body', async () => {
    (http.put as jest.Mock).mockResolvedValue({ data: null });
    const rq = { nameEn: 'A', dob: '2010-01-01' };
    await repo.updateIdentity('s1', rq as never);
    expect(http.put).toHaveBeenCalledWith('/api/swimmers/s1/identity', { body: rq });
  });

  it('createExam POSTs /api/swimmers/{id}/medical-exams with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: {} });
    const rq = { examDate: '2026-09-19', hemoglobin: 15, heightCm: 183, weightKg: 75, internalMedId: 'f1', heartAssessId: 'f1', spineAssessId: 'f1' };
    await repo.createExam('s1', rq as never);
    expect(http.post).toHaveBeenCalledWith('/api/swimmers/s1/medical-exams', { body: rq });
  });

  it('listExams GETs the exams endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.listExams('s1');
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/s1/medical-exams');
  });

  it('updateExam PUTs the exam endpoint with the body', async () => {
    (http.put as jest.Mock).mockResolvedValue({ data: {} });
    const rq = { examDate: '2026-09-19', hemoglobin: 15, heightCm: 183, weightKg: 75, internalMedId: 'f1', heartAssessId: 'f1', spineAssessId: 'f1' };
    await repo.updateExam('s1', 'e1', rq as never);
    expect(http.put).toHaveBeenCalledWith('/api/swimmers/s1/medical-exams/e1', { body: rq });
  });

  it('deleteExam DELETEs the exam endpoint', async () => {
    ((http as unknown as { delete: jest.Mock }).delete) = jest.fn().mockResolvedValue({ data: null });
    await repo.deleteExam('s1', 'e1');
    expect((http as unknown as { delete: jest.Mock }).delete).toHaveBeenCalledWith('/api/swimmers/s1/medical-exams/e1');
  });

  it('getGuardians GETs the guardians endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ successStatus: true, data: { father: null, mother: null } });
    await repo.getGuardians('sw1');
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/sw1/guardians');
  });

  it('upsertGuardians PUTs to the guardians endpoint', async () => {
    const rq = {
      father: { name: 'Hassan Ali', nationalId: '27001010123456', phone: '+201009876543' },
      mother: { name: 'Fatima Ibrahim', nationalId: '27505050123456', phone: '+201005554444' },
    };
    (http.put as jest.Mock).mockResolvedValue({ successStatus: true, data: null });
    await repo.upsertGuardians('sw1', rq);
    expect(http.put).toHaveBeenCalledWith('/api/swimmers/sw1/guardians', { body: rq });
  });
});
