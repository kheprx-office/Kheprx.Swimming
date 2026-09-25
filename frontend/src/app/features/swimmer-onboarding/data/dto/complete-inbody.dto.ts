import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { OnboardingResultDtoRs } from './complete-identity-vitals.dto';

export interface CompleteInBodyDtoRq {
  heightCm: number;
  weightKg: number;
  fatPct: number;
  musclePct: number;
  waterPct: number;
  boneDensity: number;
  bodyDensity: number;
}
export interface CompleteInBodyItemDtoRs extends BaseResponseRs<OnboardingResultDtoRs> {}
