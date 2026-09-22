import { toFeedbackEntry, toFeedbackEntryList } from '@features/swimmer-profile/data/dto/feedback-entry.mapper';
import { FeedbackEntryDtoRs } from '@features/swimmer-profile/data/dto/feedback-entry.dto';

const DTO: FeedbackEntryDtoRs = {
  id: 'f1', swimmerId: 's1', rating: 5, categoryId: 'c1', comment: 'Great rhythm',
  authorId: 'u1', authorNameEn: 'Coach Omar', authorNameAr: 'الكابتن عمر', entryDate: '2024-10-22',
};

describe('feedback-entry.mapper', () => {
  it('maps an entry (keeps author names, drops ids we do not render)', () => {
    const m = toFeedbackEntry(DTO);
    expect(m.id).toBe('f1');
    expect(m.rating).toBe(5);
    expect(m.categoryId).toBe('c1');
    expect(m.comment).toBe('Great rhythm');
    expect(m.authorNameEn).toBe('Coach Omar');
    expect(m.entryDate).toBe('2024-10-22');
    expect((m as unknown as Record<string, unknown>).swimmerId).toBeUndefined();
    expect((m as unknown as Record<string, unknown>).authorId).toBeUndefined();
  });

  it('maps a list', () => {
    expect(toFeedbackEntryList([DTO])).toHaveLength(1);
  });
});
