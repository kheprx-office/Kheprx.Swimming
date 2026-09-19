// swimmer-profile.dto.ts — swimmer profile response DTOs (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface CodedRefDtoRs { id: string; code: string; nameEn: string; nameAr: string | null; }

export interface SwimmerIdentityDtoRs {
  id: string; uid: string; nameEn: string; nameAr: string | null;
  dob: string | null; age: number | null; genderCode: string; phone: string | null;
  trainingClubNameEn: string | null; trainingClubNameAr: string | null;
}

export interface SwimmerVitalsDtoRs {
  id: string; examDate: string; bloodType: CodedRefDtoRs | null;
  hemoglobin: number; heightCm: number; weightKg: number;
  internalMed: CodedRefDtoRs; heartAssess: CodedRefDtoRs; spineAssess: CodedRefDtoRs;
}

export interface SwimmerProfileDtoRs { identity: SwimmerIdentityDtoRs; vitals: SwimmerVitalsDtoRs | null; }
export interface SwimmerProfileItemDtoRs extends BaseResponseRs<SwimmerProfileDtoRs> {}
export interface MedicalExamListDtoRs extends BaseResponseRs<SwimmerVitalsDtoRs[]> {}
export interface DeleteExamItemDtoRs extends BaseResponseRs<unknown> {}

export function isSwimmerProfileDtoRsValid(dto: unknown): dto is SwimmerProfileDtoRs {
  const d = dto as SwimmerProfileDtoRs;
  return !!d && !!d.identity
    && typeof d.identity.id === 'string' && typeof d.identity.uid === 'string'
    && typeof d.identity.nameEn === 'string' && typeof d.identity.genderCode === 'string';
}

export function isVitalsListValid(data: unknown): data is SwimmerVitalsDtoRs[] {
  return Array.isArray(data) && data.every(
    (x) => x != null && typeof x === 'object'
      && typeof (x as SwimmerVitalsDtoRs).id === 'string'
      && typeof (x as SwimmerVitalsDtoRs).hemoglobin === 'number');
}
