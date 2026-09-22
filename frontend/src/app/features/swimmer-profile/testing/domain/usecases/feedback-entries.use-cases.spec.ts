import { TestBed } from '@angular/core/testing';
import { ListFeedbackEntriesUseCase } from '@features/swimmer-profile/domain/usecases/list-feedback-entries.use-case';
import { CreateFeedbackEntryUseCase } from '@features/swimmer-profile/domain/usecases/create-feedback-entry.use-case';
import { UpdateFeedbackEntryUseCase } from '@features/swimmer-profile/domain/usecases/update-feedback-entry.use-case';
import { DeleteFeedbackEntryUseCase } from '@features/swimmer-profile/domain/usecases/delete-feedback-entry.use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

const ENTRY = {
  id: 'f1', swimmerId: 's1', rating: 5, categoryId: 'c1', comment: 'x',
  authorId: 'u1', authorNameEn: 'Coach Omar', authorNameAr: null, entryDate: '2024-10-22',
};
const RQ = { rating: 5, categoryId: 'c1', comment: 'x' };

describe('feedback use-cases', () => {
  const repo = {
    getFeedbackEntries: jest.fn(),
    createFeedbackEntry: jest.fn(),
    updateFeedbackEntry: jest.fn(),
    deleteFeedbackEntry: jest.fn(),
  } as any;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        ListFeedbackEntriesUseCase,
        CreateFeedbackEntryUseCase,
        UpdateFeedbackEntryUseCase,
        DeleteFeedbackEntryUseCase,
        { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo },
      ],
    });
  });

  it('list maps valid entries', async () => {
    repo.getFeedbackEntries.mockResolvedValue({ successStatus: true, data: [ENTRY] });
    const res = await TestBed.inject(ListFeedbackEntriesUseCase).run('s1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data[0].comment).toBe('x');
  });

  it('list fails on invalid payload', async () => {
    repo.getFeedbackEntries.mockResolvedValue({ successStatus: true, data: [{ id: 5 }] });
    const res = await TestBed.inject(ListFeedbackEntriesUseCase).run('s1');
    expect(res.ok).toBe(false);
  });

  it('create maps the returned entry', async () => {
    repo.createFeedbackEntry.mockResolvedValue({ successStatus: true, data: ENTRY });
    const res = await TestBed.inject(CreateFeedbackEntryUseCase).run({ id: 's1', rq: RQ });
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data.id).toBe('f1');
    expect(repo.createFeedbackEntry).toHaveBeenCalledWith('s1', RQ);
  });

  it('update maps the returned entry', async () => {
    repo.updateFeedbackEntry.mockResolvedValue({ successStatus: true, data: ENTRY });
    const res = await TestBed.inject(UpdateFeedbackEntryUseCase).run({ id: 's1', entryId: 'f1', rq: RQ });
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data.id).toBe('f1');
    expect(repo.updateFeedbackEntry).toHaveBeenCalledWith('s1', 'f1', RQ);
  });

  it('delete calls the repo', async () => {
    repo.deleteFeedbackEntry.mockResolvedValue({ successStatus: true, data: null });
    const res = await TestBed.inject(DeleteFeedbackEntryUseCase).run({ id: 's1', entryId: 'f1' });
    expect(res.ok).toBe(true);
    expect(repo.deleteFeedbackEntry).toHaveBeenCalledWith('s1', 'f1');
  });
});
