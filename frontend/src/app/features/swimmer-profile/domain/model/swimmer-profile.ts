import { LookupItem } from '@features/reference/domain/model/reference';

export interface SwimmerIdentity {
  id: string;
  uid: string;
  nameEn: string;
  nameAr: string | null;
  dob: string | null;       // ISO yyyy-mm-dd
  age: number | null;
  genderCode: string;
  phone: string | null;
  trainingClubNameEn: string | null;
  trainingClubNameAr: string | null;
}

export interface SwimmerVitals {
  id: string;
  examDate: string;         // ISO yyyy-mm-dd
  bloodType: LookupItem | null;
  hemoglobin: number;
  heightCm: number;
  weightKg: number;
  internalMed: LookupItem;
  heartAssess: LookupItem;
  spineAssess: LookupItem;
}

export interface SwimmerProfile {
  identity: SwimmerIdentity;
  vitals: SwimmerVitals | null;
}
