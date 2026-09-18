// role.dto.ts — roles list response DTOs (API_FLOW convention).
// RoleDtoRs fields are raw/untrusted transport — isRoleDtoRsValid guards them
// before the use case maps to the domain model.
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface RoleDtoRs {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string | null;
}

export interface RolesListDtoRs extends BaseResponseRs<RoleDtoRs[]> {}

export function isRoleDtoRsValid(dto: RoleDtoRs): boolean {
  return (
    typeof dto.id === 'string' && dto.id.length > 0 &&
    typeof dto.code === 'string' && dto.code.length > 0 &&
    typeof dto.nameEn === 'string' && dto.nameEn.length > 0 &&
    (dto.nameAr === null || typeof dto.nameAr === 'string')
  );
}
