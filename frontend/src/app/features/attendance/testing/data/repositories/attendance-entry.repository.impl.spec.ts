import { TestBed } from '@angular/core/testing';
import { AttendanceEntryRepositoryImpl } from '@features/attendance/data/repositories/attendance-entry.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('AttendanceEntryRepositoryImpl', () => {
  const http = { get: jest.fn(), put: jest.fn() } as unknown as HttpClientService;
  let repo: AttendanceEntryRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [AttendanceEntryRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(AttendanceEntryRepositoryImpl);
  });

  it('getSession GETs the session endpoint with the date param', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: null });
    await repo.getSession('2026-09-23');
    expect(http.get).toHaveBeenCalledWith('/api/attendance-records/session', { params: { date: '2026-09-23' } });
  });

  it('saveSession PUTs the payload as the body', async () => {
    (http.put as jest.Mock).mockResolvedValue({ data: null });
    const rq = { date: '2026-09-23', entries: [{ swimmerId: 's1', statusId: 'st1', coachNote: null }] };
    await repo.saveSession(rq);
    expect(http.put).toHaveBeenCalledWith('/api/attendance-records/session', { body: rq });
  });
});
