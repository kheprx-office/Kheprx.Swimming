import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface FeedbackEntryDtoRs {
  id: string;
  swimmerId: string;
  rating: number;
  categoryId: string;
  comment: string;
  authorId: string;
  authorNameEn: string;
  authorNameAr: string | null;
  entryDate: string;
}
export interface FeedbackEntryListDtoRs extends BaseResponseRs<FeedbackEntryDtoRs[]> {}
export interface FeedbackEntryItemDtoRs extends BaseResponseRs<FeedbackEntryDtoRs> {}
export interface DeleteFeedbackEntryItemDtoRs extends BaseResponseRs<unknown> {}

export interface CreateFeedbackEntryDtoRq {
  rating: number;
  categoryId: string;
  comment: string;
}

export function isFeedbackEntryDtoRsValid(x: unknown): x is FeedbackEntryDtoRs {
  const d = x as FeedbackEntryDtoRs;
  if (!d || typeof d !== 'object') return false;
  return typeof d.id === 'string'
    && typeof d.rating === 'number'
    && typeof d.categoryId === 'string'
    && typeof d.comment === 'string'
    && typeof d.authorNameEn === 'string'
    && typeof d.entryDate === 'string';
}

export function isFeedbackEntryListValid(data: unknown): data is FeedbackEntryDtoRs[] {
  return Array.isArray(data) && data.every(isFeedbackEntryDtoRsValid);
}
