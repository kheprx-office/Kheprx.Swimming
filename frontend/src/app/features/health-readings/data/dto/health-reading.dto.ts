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
