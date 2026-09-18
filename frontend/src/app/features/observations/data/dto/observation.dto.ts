// observation.dto.ts — observation request/response DTOs (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface ObservationDtoRs {
  id: string;
  swimmerId: string;
  categoryId: string;
  fieldLabel: string;
  value: string;
  observedDate: string;
  recordedBy: string;
}

export interface ObservationItemDtoRs extends BaseResponseRs<ObservationDtoRs> {}

export interface CreateObservationDtoRq {
  swimmerId: string;
  categoryId: string;
  fieldLabel: string;
  value: string;
}

export function isObservationDtoRsValid(dto: unknown): dto is ObservationDtoRs {
  const d = dto as ObservationDtoRs;
  return !!d
    && typeof d.id === 'string'
    && typeof d.swimmerId === 'string'
    && typeof d.categoryId === 'string'
    && typeof d.fieldLabel === 'string'
    && typeof d.value === 'string'
    && typeof d.observedDate === 'string'
    && typeof d.recordedBy === 'string';
}
