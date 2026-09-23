import { TestBed } from '@angular/core/testing';
import { ListAttendanceRecordsUseCase } from '@features/swimmer-profile/domain/usecases/list-attendance-records.use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

const DTO = {
  id: 'a1', swimmerId: 's1', sessionDate: '2026-09-09', statusId: 'st1',
  coachNoteEn: 'Excused', coachNoteAr: 'بعذر', recordedBy: 'u1',
  recordedByNameEn: 'Coach Layla', recordedByNameAr: 'الكابتن ليلى',
};

describe('ListAttendanceRecordsUseCase', () => {
  const repo = { getAttendanceRecords: jest.fn() } as any;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [
      ListAttendanceRecordsUseCase,
      { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo },
    ] });
  });

  it('maps valid attendance records', async () => {
    repo.getAttendanceRecords.mockResolvedValue({ successStatus: true, data: [DTO] });
    const res = await TestBed.inject(ListAttendanceRecordsUseCase).run('s1');
    expect(res.ok).toBe(true);
    if (res.ok) {
      expect(res.data).toHaveLength(1);
      expect(res.data[0].statusId).toBe('st1');
      expect(res.data[0].recordedByNameEn).toBe('Coach Layla');
      expect((res.data[0] as unknown as Record<string, unknown>).swimmerId).toBeUndefined();
      expect((res.data[0] as unknown as Record<string, unknown>).recordedBy).toBeUndefined();
    }
  });

  it('fails on invalid payload', async () => {
    repo.getAttendanceRecords.mockResolvedValue({ successStatus: true, data: [{ id: 5 }] });
    const res = await TestBed.inject(ListAttendanceRecordsUseCase).run('s1');
    expect(res.ok).toBe(false);
  });
});
