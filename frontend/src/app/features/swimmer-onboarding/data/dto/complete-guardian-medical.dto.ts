import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { OnboardingResultDtoRs } from './complete-identity-vitals.dto';

export interface GuardianInputDtoRq {
  name: string;
  nationalId: string;
  phone: string;
}
export interface OnboardingMedicalItemDtoRq {
  categoryId: string;
  fieldLabel: string;
  value: string;
}
export interface CompleteGuardianMedicalDtoRq {
  father: GuardianInputDtoRq;
  mother: GuardianInputDtoRq;
  medical: OnboardingMedicalItemDtoRq[];
}
export interface CompleteGuardianMedicalItemDtoRs extends BaseResponseRs<OnboardingResultDtoRs> {}
