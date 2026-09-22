// health-reading.dto.ts — health-reading request/response DTOs (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface HealthReadingDtoRs {
  id: string;
  swimmerId: string;
  medicalTestId: string;
  value: number;
  readingDate: string;
  recordedBy: string;
  status: string;
}

export interface HealthReadingItemDtoRs extends BaseResponseRs<HealthReadingDtoRs> {}

export interface CreateHealthReadingDtoRq {
  swimmerId: string;
  medicalTestId: string;
  value: number;
}

export function isHealthReadingDtoRsValid(dto: unknown): dto is HealthReadingDtoRs {
  const d = dto as HealthReadingDtoRs;
  return !!d
    && typeof d.id === 'string'
    && typeof d.swimmerId === 'string'
    && typeof d.medicalTestId === 'string'
    && typeof d.value === 'number'
    && typeof d.readingDate === 'string'
    && typeof d.recordedBy === 'string'
    && typeof d.status === 'string';
}

export interface HealthReadingRowDtoRs {
  id: string;
  medicalTestId: string;
  testNameEn: string;
  testNameAr: string;
  unit: string;
  value: number;
  lowerBound: number;
  upperBound: number;
  readingDate: string;
  status: string;
}

export interface HealthReadingListDtoRs extends BaseResponseRs<HealthReadingRowDtoRs[]> {}
export interface HealthReadingRowItemDtoRs extends BaseResponseRs<HealthReadingRowDtoRs> {}
export interface DeleteHealthReadingItemDtoRs extends BaseResponseRs<unknown> {}

export interface UpdateHealthReadingDtoRq {
  value: number;
}

export function isHealthReadingRowDtoRsValid(x: unknown): x is HealthReadingRowDtoRs {
  const d = x as HealthReadingRowDtoRs;
  return !!d && typeof d === 'object'
    && typeof d.id === 'string'
    && typeof d.medicalTestId === 'string'
    && typeof d.testNameEn === 'string'
    && typeof d.testNameAr === 'string'
    && typeof d.unit === 'string'
    && typeof d.value === 'number'
    && typeof d.lowerBound === 'number'
    && typeof d.upperBound === 'number'
    && typeof d.readingDate === 'string'
    && typeof d.status === 'string';
}

export function isHealthReadingRowListValid(data: unknown): data is HealthReadingRowDtoRs[] {
  return Array.isArray(data) && data.every(isHealthReadingRowDtoRsValid);
}
