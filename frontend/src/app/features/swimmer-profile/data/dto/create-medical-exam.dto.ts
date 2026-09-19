import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { SwimmerVitalsDtoRs } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';

export interface CreateMedicalExamDtoRq {
  examDate: string;
  bloodTypeId?: string | null;
  hemoglobin: number;
  heightCm: number;
  weightKg: number;
  internalMedId: string;
  heartAssessId: string;
  spineAssessId: string;
}
export interface CreatedVitalsItemDtoRs extends BaseResponseRs<SwimmerVitalsDtoRs> {}
