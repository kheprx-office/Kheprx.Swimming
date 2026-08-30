// current-user.dto.ts — current-user response DTOs (API_FLOW convention).
// CurrentUserDtoRs fields are raw/untrusted transport — isCurrentUserDtoRsValid
// guards them before the use case maps to the domain model.
import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { ROLE_LABELS } from '@core/domain/roles';

export interface CurrentUserDtoRs {
  userId: string;
  email: string;
  fullName: string;
  role: string;
  phone: string | null;
  gender: string | null;
  age: number | null;
}

export interface CurrentUserItemDtoRs extends BaseResponseRs<CurrentUserDtoRs> {}

function isNullableString(v: unknown): boolean {
  return v === null || typeof v === 'string';
}
function isNullableNumber(v: unknown): boolean {
  return v === null || typeof v === 'number';
}

export function isCurrentUserDtoRsValid(dto: CurrentUserDtoRs): boolean {
  return (
    typeof dto.userId === 'string' && dto.userId.length > 0 &&
    typeof dto.email === 'string' &&
    typeof dto.fullName === 'string' &&
    typeof dto.role === 'string' &&
    Object.prototype.hasOwnProperty.call(ROLE_LABELS, dto.role) &&
    isNullableString(dto.phone) &&
    (dto.gender === null || dto.gender === 'male' || dto.gender === 'female') &&
    isNullableNumber(dto.age)
  );
}
