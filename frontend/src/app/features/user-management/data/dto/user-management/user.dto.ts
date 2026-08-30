// user.dto.ts — user resource response DTOs (API_FLOW convention).
// UserDtoRs fields are raw/untrusted transport — isUserDtoRsValid guards them
// before the use case maps to the domain model.
import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { ROLE_LABELS } from '@core/domain/roles';

export interface UserProfileDtoRs {
  monthlySalary: number | null;
  dailyWage: number | null;
}

export interface UserDtoRs {
  id: string;
  code: string | null;
  fullName: string;
  email: string | null;
  phone: string | null;
  gender: string | null;
  age: number | null;
  nid: string;
  role: string;
  status: string;
  mustChangePassword: boolean;
  profile: UserProfileDtoRs | null;
}

export interface UsersDtoRs extends BaseResponseRs<UserDtoRs[]> {}
export interface UserItemDtoRs extends BaseResponseRs<UserDtoRs> {}

function isNullableString(v: unknown): boolean {
  return v === null || typeof v === 'string';
}
function isNullableNumber(v: unknown): boolean {
  return v === null || typeof v === 'number';
}

function isUserProfileDtoRsValid(p: UserProfileDtoRs | null): boolean {
  if (p === null) return true;
  return (
    typeof p === 'object' &&
    isNullableNumber(p.monthlySalary) &&
    isNullableNumber(p.dailyWage)
  );
}

export function isUserDtoRsValid(dto: UserDtoRs): boolean {
  return (
    typeof dto.id === 'string' && dto.id.length > 0 &&
    isNullableString(dto.code) &&
    typeof dto.fullName === 'string' &&
    isNullableString(dto.email) &&
    isNullableString(dto.phone) &&
    (dto.gender === null || dto.gender === 'male' || dto.gender === 'female') &&
    isNullableNumber(dto.age) &&
    typeof dto.nid === 'string' && dto.nid.length > 0 &&
    typeof dto.role === 'string' &&
    Object.prototype.hasOwnProperty.call(ROLE_LABELS, dto.role) &&
    (dto.status === 'active' || dto.status === 'disabled') &&
    typeof dto.mustChangePassword === 'boolean' &&
    isUserProfileDtoRsValid(dto.profile)
  );
}
