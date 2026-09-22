export interface HealthReadingListItem {
  id: string;
  medicalTestId: string;
  testNameEn: string;
  testNameAr: string;
  unit: string;
  value: number;
  lowerBound: number;
  upperBound: number;
  readingDate: string;
  status: string;
}
