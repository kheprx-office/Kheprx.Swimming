import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { OnboardingResultDtoRs } from './complete-identity-vitals.dto';

export interface CompletePhysiologicalDtoRq {
  rightArmCm: number;
  leftArmCm: number;
  rightLegCm: number;
  leftLegCm: number;
  torsoCm: number;
  bustDiameterCm: number;
  waistDiameterCm: number;
}
export interface CompletePhysiologicalItemDtoRs extends BaseResponseRs<OnboardingResultDtoRs> {}
