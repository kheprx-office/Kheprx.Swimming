export interface HealthReading {
  id: string;
  swimmerId: string;
  medicalTestId: string;
  value: number;
  readingDate: string;
  recordedBy: string;
  status: string;
}
