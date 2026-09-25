// onboarding.ts — swimmer first-login onboarding domain models.
export interface OnboardingPrefill {
  uid: string;
  nameEn: string;
  nameAr: string | null;
  genderId: string | null;
  dob: string | null;
  trainingClubId: string | null;
  phone: string | null;
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
  phone: string | null;
}

export interface OnboardingResult { mustChangePassword: boolean; }

export interface GuardianInput {
  name: string;
  nationalId: string;
  phone: string;
}

export interface OnboardingMedicalItem {
  categoryId: string;
  fieldLabel: string;
  value: string;
}

export interface GuardianMedicalSubmission {
  father: GuardianInput;
  mother: GuardianInput;
  medical: OnboardingMedicalItem[];
}

export interface PhysiologicalSubmission {
  rightArmCm: number;
  leftArmCm: number;
  rightLegCm: number;
  leftLegCm: number;
  torsoCm: number;
  bustDiameterCm: number;
  waistDiameterCm: number;
}

export interface InBodySubmission {
  heightCm: number;
  weightKg: number;
  fatPct: number;
  musclePct: number;
  waterPct: number;
  boneDensity: number;
  bodyDensity: number;
}
