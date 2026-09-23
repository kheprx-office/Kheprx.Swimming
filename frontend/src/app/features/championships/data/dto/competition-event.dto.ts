import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface CompetitionEventDtoRs {
  id: string;
  nameEn: string;
  nameAr: string | null;
  startDate: string;
  endDate: string;
  locationEn: string;
  locationAr: string | null;
  statusId: string;
  statusCode: string;
  statusNameEn: string;
  statusNameAr: string | null;
}
export interface CompetitionEventListDtoRs extends BaseResponseRs<CompetitionEventDtoRs[]> {}
export interface CompetitionEventItemDtoRs extends BaseResponseRs<CompetitionEventDtoRs> {}

/** Create-championship request body. The single name/location map to the current-language column server-side. */
export interface CreateChampionshipRq {
  name: string;
  startDate: string;   // 'YYYY-MM-DD'
  endDate: string;     // 'YYYY-MM-DD'
  location: string;
}

export function isCompetitionEventDtoRsValid(x: unknown): x is CompetitionEventDtoRs {
  const d = x as CompetitionEventDtoRs;
  if (!d || typeof d !== 'object') return false;
  return typeof d.id === 'string'
    && typeof d.nameEn === 'string'
    && typeof d.startDate === 'string'
    && typeof d.endDate === 'string'
    && typeof d.locationEn === 'string'
    && typeof d.statusId === 'string'
    && typeof d.statusCode === 'string';
}

export function isCompetitionEventListValid(data: unknown): data is CompetitionEventDtoRs[] {
  return Array.isArray(data) && data.every(isCompetitionEventDtoRsValid);
}
