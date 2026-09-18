import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface CreateCoachDtoRq {
  role: 'captain' | 'head_coach';
  nameEn: string;
  username: string;
  email: string;
  nationalId: string;
  genderId: string;
  dob: string; // yyyy-mm-dd
  phone: string;
  nameAr?: string;
}

export interface CreatedCoachDtoRs {
  id: string;
  username: string;
  nameEn: string;
  role: string;
  temporaryPassword: string;
}

export interface CreatedCoachItemDtoRs extends BaseResponseRs<CreatedCoachDtoRs> {}

export function isCreatedCoachDtoRsValid(dto: unknown): dto is CreatedCoachDtoRs {
  const d = dto as CreatedCoachDtoRs;
  return !!d && typeof d.id === 'string' && typeof d.username === 'string'
    && typeof d.nameEn === 'string' && typeof d.role === 'string'
    && typeof d.temporaryPassword === 'string';
}
