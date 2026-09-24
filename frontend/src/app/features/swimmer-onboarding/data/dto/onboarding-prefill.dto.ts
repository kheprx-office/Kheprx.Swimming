import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface OnboardingPrefillDtoRs {
  uid: string;
  nameEn: string;
  nameAr: string | null;
  genderId: string | null;
  dob: string | null;
  trainingClubId: string | null;
}
export interface OnboardingPrefillItemDtoRs extends BaseResponseRs<OnboardingPrefillDtoRs> {}

export function isOnboardingPrefillValid(dto: unknown): dto is OnboardingPrefillDtoRs {
  const d = dto as OnboardingPrefillDtoRs;
  return !!d && typeof d.uid === 'string' && typeof d.nameEn === 'string';
}
