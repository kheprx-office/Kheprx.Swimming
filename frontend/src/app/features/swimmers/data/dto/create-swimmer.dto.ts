import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface CreateSwimmerDtoRq {
  nameEn: string;
  username: string;
  trainingClubId: string;
  genderId: string;
  dob: string; // ISO date (yyyy-mm-dd)
  bloodTypeId: string;
  strokeIds: string[];
  nameAr?: string;
  email?: string;
  phone?: string;
  representChampionshipClubId?: string;
}

export interface CreatedSwimmerDtoRs {
  id: string;
  uid: string;
  username: string;
  nameEn: string;
  temporaryPassword: string;
}

export interface CreatedSwimmerItemDtoRs extends BaseResponseRs<CreatedSwimmerDtoRs> {}

export function isCreatedSwimmerDtoRsValid(dto: unknown): dto is CreatedSwimmerDtoRs {
  const d = dto as CreatedSwimmerDtoRs;
  return !!d && typeof d.id === 'string' && typeof d.uid === 'string'
    && typeof d.username === 'string' && typeof d.nameEn === 'string'
    && typeof d.temporaryPassword === 'string';
}
