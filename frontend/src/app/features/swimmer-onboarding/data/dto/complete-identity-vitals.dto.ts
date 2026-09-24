import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface CompleteIdentityVitalsDtoRq {
  nameEn: string;
  nameAr: string | null;
  genderId: string;
  dob: string;
  trainingClubId: string;
  examDate: string;
  bloodTypeId: string | null;
  hemoglobin: number;
  heightCm: number;
  weightKg: number;
  internalMedId: string;
  heartAssessId: string;
  spineAssessId: string;
}
export interface OnboardingResultDtoRs { mustChangePassword: boolean; }
export interface CompleteIdentityVitalsItemDtoRs extends BaseResponseRs<OnboardingResultDtoRs> {}
