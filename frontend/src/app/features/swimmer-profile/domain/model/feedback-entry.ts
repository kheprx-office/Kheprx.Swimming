export interface FeedbackEntry {
  id: string;
  rating: number;
  categoryId: string;
  comment: string;
  authorNameEn: string;
  authorNameAr: string | null;
  entryDate: string;
}
