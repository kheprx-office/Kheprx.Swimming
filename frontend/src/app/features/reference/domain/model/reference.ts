// reference.ts — a resolved lookup option (value = id; label = nameEn/nameAr).
export interface LookupItem {
  id: string;
  code?: string;
  nameEn: string;
  nameAr: string | null;
}
