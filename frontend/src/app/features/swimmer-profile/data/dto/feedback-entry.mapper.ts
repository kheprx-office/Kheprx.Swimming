import { FeedbackEntryDtoRs } from '@features/swimmer-profile/data/dto/feedback-entry.dto';
import { FeedbackEntry } from '@features/swimmer-profile/domain/model/feedback-entry';

export function toFeedbackEntry(d: FeedbackEntryDtoRs): FeedbackEntry {
  return {
    id: d.id,
    rating: d.rating,
    categoryId: d.categoryId,
    comment: d.comment,
    authorNameEn: d.authorNameEn,
    authorNameAr: d.authorNameAr,
    entryDate: d.entryDate,
  };
}

export function toFeedbackEntryList(list: FeedbackEntryDtoRs[]): FeedbackEntry[] {
  return list.map(toFeedbackEntry);
}
