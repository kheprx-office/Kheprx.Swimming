// swimmer-list.dto.ts — swimmer roster response DTO (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface SwimmerListItemDtoRs {
  id: string;
  uid: string;
  nameEn: string;
  nameAr?: string | null;
  clubNameEn?: string | null;
  clubNameAr?: string | null;
  genderCode: string;
  age?: number | null;
}

export interface SwimmerListItemsDtoRs extends BaseResponseRs<SwimmerListItemDtoRs[]> {}

export function isSwimmerListItemDtoRsValid(dto: unknown): dto is SwimmerListItemDtoRs {
  const d = dto as SwimmerListItemDtoRs;
  return !!d && typeof d.id === 'string' && typeof d.uid === 'string'
    && typeof d.nameEn === 'string' && typeof d.genderCode === 'string';
}
