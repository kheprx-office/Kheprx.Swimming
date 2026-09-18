// reference.dto.ts — reference lookup response DTOs (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface CodedLookupDtoRs {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string | null;
}

export interface ClubDtoRs {
  id: string;
  nameEn: string;
  nameAr: string | null;
}

export interface CodedLookupListDtoRs extends BaseResponseRs<CodedLookupDtoRs[]> {}
export interface ClubListDtoRs extends BaseResponseRs<ClubDtoRs[]> {}

function isNonEmptyString(v: unknown): v is string {
  return typeof v === 'string' && v.length > 0;
}

export function isCodedLookupListValid(data: unknown): data is CodedLookupDtoRs[] {
  return (
    Array.isArray(data) &&
    data.every(
      (x) =>
        x != null &&
        typeof x === 'object' &&
        isNonEmptyString((x as CodedLookupDtoRs).id) &&
        isNonEmptyString((x as CodedLookupDtoRs).code) &&
        isNonEmptyString((x as CodedLookupDtoRs).nameEn),
    )
  );
}

export function isClubListValid(data: unknown): data is ClubDtoRs[] {
  return (
    Array.isArray(data) &&
    data.every(
      (x) =>
        x != null &&
        typeof x === 'object' &&
        isNonEmptyString((x as ClubDtoRs).id) &&
        isNonEmptyString((x as ClubDtoRs).nameEn),
    )
  );
}
