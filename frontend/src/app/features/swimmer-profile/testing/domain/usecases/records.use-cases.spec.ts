import { TestBed } from '@angular/core/testing';
import { ListRecordsUseCase } from '@features/swimmer-profile/domain/usecases/list-records.use-case';
import { UpdateRecordUseCase } from '@features/swimmer-profile/domain/usecases/update-record.use-case';
import { DeleteRecordUseCase } from '@features/swimmer-profile/domain/usecases/delete-record.use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

const REC = { id: 'o1', swimmerId: 's1', categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe', observedDate: '2026-09-20T10:00:00Z', recordedBy: 'u1' };
const RQ = { categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe' };

describe('records use cases', () => {
  const repo = { listRecords: jest.fn(), updateRecord: jest.fn(), deleteRecord: jest.fn() } as any;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [
      ListRecordsUseCase, UpdateRecordUseCase, DeleteRecordUseCase,
      { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo },
    ] });
  });

  it('list maps valid records', async () => {
    repo.listRecords.mockResolvedValue({ successStatus: true, data: [REC] });
    const res = await TestBed.inject(ListRecordsUseCase).run('s1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data[0].fieldLabel).toBe('Penicillin');
  });

  it('list fails on invalid payload', async () => {
    repo.listRecords.mockResolvedValue({ successStatus: true, data: [{ id: 5 }] });
    const res = await TestBed.inject(ListRecordsUseCase).run('s1');
    expect(res.ok).toBe(false);
  });

  it('update maps the returned record', async () => {
    repo.updateRecord.mockResolvedValue({ successStatus: true, data: REC });
    const res = await TestBed.inject(UpdateRecordUseCase).run({ recordId: 'o1', rq: RQ });
    expect(res.ok).toBe(true);
    expect(repo.updateRecord).toHaveBeenCalledWith('o1', RQ);
  });

  it('delete calls the repo', async () => {
    repo.deleteRecord.mockResolvedValue({ successStatus: true, data: null });
    const res = await TestBed.inject(DeleteRecordUseCase).run({ recordId: 'o1' });
    expect(res.ok).toBe(true);
    expect(repo.deleteRecord).toHaveBeenCalledWith('o1');
  });
});
