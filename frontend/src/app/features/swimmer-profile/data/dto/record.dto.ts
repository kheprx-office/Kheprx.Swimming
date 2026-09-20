import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface RecordDtoRs {
  id: string;
  swimmerId: string;
  categoryId: string;
  fieldLabel: string;
  value: string;
  observedDate: string;
  recordedBy: string;
}
export interface RecordListDtoRs extends BaseResponseRs<RecordDtoRs[]> {}
export interface RecordItemDtoRs extends BaseResponseRs<RecordDtoRs> {}
export interface DeleteRecordItemDtoRs extends BaseResponseRs<unknown> {}

export interface UpdateRecordDtoRq {
  categoryId: string;
  fieldLabel: string;
  value: string;
}

export function isRecordDtoRsValid(x: unknown): x is RecordDtoRs {
  const d = x as RecordDtoRs;
  if (!d || typeof d !== 'object') return false;
  return typeof d.id === 'string'
    && typeof d.swimmerId === 'string'
    && typeof d.categoryId === 'string'
    && typeof d.fieldLabel === 'string'
    && typeof d.value === 'string'
    && typeof d.observedDate === 'string';
}

export function isRecordListValid(data: unknown): data is RecordDtoRs[] {
  return Array.isArray(data) && data.every(isRecordDtoRsValid);
}
