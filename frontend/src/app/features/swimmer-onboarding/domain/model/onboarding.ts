// onboarding.ts — swimmer first-login onboarding domain models.
export interface OnboardingPrefill {
  uid: string;
  nameEn: string;
  nameAr: string | null;
  genderId: string | null;
  dob: string | null;
  trainingClubId: string | null;
}

export interface IdentityVitalsSubmission {
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

export interface OnboardingResult { mustChangePassword: boolean; }
