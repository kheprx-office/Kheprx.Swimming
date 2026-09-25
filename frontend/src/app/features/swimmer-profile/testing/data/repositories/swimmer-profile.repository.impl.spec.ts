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

  it('getBodyMeasurement GETs the latest endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ successStatus: true, data: { latest: null } });
    await repo.getBodyMeasurement('sw1');
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/sw1/body-measurements/latest');
  });

  it('createBodyMeasurement POSTs to the collection endpoint with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ successStatus: true, data: null });
    const rq = { rightArmCm: 78.5, leftArmCm: 78.2, rightLegCm: 96.2, leftLegCm: 96.0, torsoCm: 52.8, bustDiameterCm: 94.0, waistDiameterCm: 76.5 };
    await repo.createBodyMeasurement('sw1', rq);
    expect(http.post).toHaveBeenCalledWith('/api/swimmers/sw1/body-measurements', { body: rq });
  });

  it('getInBodyReadings GETs the readings endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ successStatus: true, data: [] });
    await repo.getInBodyReadings('sw1');
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/sw1/inbody-readings');
  });

  it('createInBodyReading POSTs to the readings endpoint', async () => {
    (http.post as jest.Mock).mockResolvedValue({ successStatus: true, data: {} });
    const rq = { readingDate: '2024-10-04', heightCm: 180, weightKg: 74, fatPct: 12.8, musclePct: 42.1, waterPct: 55.3, boneDensity: 1.35, bodyDensity: 1.07 };
    await repo.createInBodyReading('sw1', rq);
    expect(http.post).toHaveBeenCalledWith('/api/swimmers/sw1/inbody-readings', { body: rq });
  });

  it('updateInBodyReading PUTs the reading endpoint', async () => {
    (http.put as jest.Mock).mockResolvedValue({ successStatus: true, data: {} });
    const rq = { readingDate: '2024-10-04', heightCm: 180, weightKg: 74, fatPct: 12.8, musclePct: 42.1, waterPct: 55.3, boneDensity: 1.35, bodyDensity: 1.07 };
    await repo.updateInBodyReading('sw1', 'r1', rq);
    expect(http.put).toHaveBeenCalledWith('/api/swimmers/sw1/inbody-readings/r1', { body: rq });
  });

  it('deleteInBodyReading DELETEs the reading endpoint', async () => {
    ((http as unknown as { delete: jest.Mock }).delete) = jest.fn().mockResolvedValue({ successStatus: true, data: null });
    await repo.deleteInBodyReading('sw1', 'r1');
    expect((http as unknown as { delete: jest.Mock }).delete).toHaveBeenCalledWith('/api/swimmers/sw1/inbody-readings/r1');
  });

  it('listRecords GETs /api/observations with swimmerId query', async () => {
    (http.get as jest.Mock).mockResolvedValue({ successStatus: true, data: [] });
    await repo.listRecords('s1');
    expect(http.get).toHaveBeenCalledWith('/api/observations?swimmerId=s1');
  });

  it('updateRecord PUTs /api/observations/{recordId} with the body', async () => {
    (http.put as jest.Mock).mockResolvedValue({ successStatus: true, data: {} });
    const rq = { categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe' };
    await repo.updateRecord('o1', rq);
    expect(http.put).toHaveBeenCalledWith('/api/observations/o1', { body: rq });
  });

  it('deleteRecord DELETEs /api/observations/{recordId}', async () => {
    ((http as unknown as { delete: jest.Mock }).delete) = jest.fn().mockResolvedValue({ successStatus: true, data: null });
    await repo.deleteRecord('o1');
    expect((http as unknown as { delete: jest.Mock }).delete).toHaveBeenCalledWith('/api/observations/o1');
  });

  it('getMySwimmerId GETs /api/swimmers/me', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: { swimmerId: 'SW-1' } });
    await repo.getMySwimmerId();
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/me');
  });

  it('getFeedbackEntries GETs /api/swimmers/{id}/feedback-entries', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.getFeedbackEntries('s1');
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/s1/feedback-entries');
  });

  it('createFeedbackEntry POSTs to /api/swimmers/{id}/feedback-entries with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: {} });
    const rq = { rating: 5, categoryId: 'c1', comment: 'x' };
    await repo.createFeedbackEntry('s1', rq);
    expect(http.post).toHaveBeenCalledWith('/api/swimmers/s1/feedback-entries', { body: rq });
  });

  it('updateFeedbackEntry PUTs to /api/swimmers/{id}/feedback-entries/{entryId} with the body', async () => {
    (http.put as jest.Mock).mockResolvedValue({ data: {} });
    const rq = { rating: 4, categoryId: 'c1', comment: 'y' };
    await repo.updateFeedbackEntry('s1', 'f1', rq);
    expect(http.put).toHaveBeenCalledWith('/api/swimmers/s1/feedback-entries/f1', { body: rq });
  });

  it('deleteFeedbackEntry DELETEs /api/swimmers/{id}/feedback-entries/{entryId}', async () => {
    ((http as unknown as { delete: jest.Mock }).delete) = jest.fn().mockResolvedValue({ data: null });
    await repo.deleteFeedbackEntry('s1', 'f1');
    expect((http as unknown as { delete: jest.Mock }).delete).toHaveBeenCalledWith('/api/swimmers/s1/feedback-entries/f1');
  });
});
