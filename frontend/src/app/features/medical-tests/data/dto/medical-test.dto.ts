// medical-test.dto.ts — medical-test catalog response/request DTOs (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface MedicalTestDtoRs {
  id: string;
  nameEn: string;
  nameAr: string;
  unit: string;
  lowerBound: number;
  upperBound: number;
  createdAt: string;
}

export interface MedicalTestListDtoRs extends BaseResponseRs<MedicalTestDtoRs[]> {}
export interface MedicalTestItemDtoRs extends BaseResponseRs<MedicalTestDtoRs> {}
export interface MedicalTestDeletedDtoRs extends BaseResponseRs<unknown> {}

export interface CreateMedicalTestDtoRq {
  nameEn: string;
  nameAr: string;
  unit: string;
  lowerBound: number;
  upperBound: number;
}

export function isMedicalTestDtoRsValid(dto: unknown): dto is MedicalTestDtoRs {
  const d = dto as MedicalTestDtoRs;
  return !!d && typeof d.id === 'string'
    && typeof d.nameEn === 'string' && typeof d.nameAr === 'string'
    && typeof d.unit === 'string'
    && typeof d.lowerBound === 'number' && typeof d.upperBound === 'number' && typeof d.createdAt === 'string';
}
