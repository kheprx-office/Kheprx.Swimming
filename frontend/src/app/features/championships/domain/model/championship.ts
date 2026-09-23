export interface Championship {
  id: string;
  nameEn: string;
  nameAr: string | null;
  startDate: string;   // 'YYYY-MM-DD'
  endDate: string;     // 'YYYY-MM-DD'
  locationEn: string;
  locationAr: string | null;
  statusId: string;
  statusCode: string;      // 'upcoming' | 'completed'
  statusNameEn: string;
  statusNameAr: string | null;
}
