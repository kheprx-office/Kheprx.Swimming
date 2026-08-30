// user.ts — user-management domain model + mappers (both directions).
import { UserRole } from '@core/domain/roles';
import { Gender } from '@core/domain/gender';
import { UserDtoRs } from '@features/user-management/data/dto/user-management/user.dto';
import { CreateUserDtoRq } from '@features/user-management/data/dto/user-management/create-user.dto';
import { UpdateUserDtoRq } from '@features/user-management/data/dto/user-management/update-user.dto';

export type UserStatus = 'active' | 'disabled';

// Gender vocabulary now lives in core; re-export so existing '.../user' importers keep working.
export { GENDER_LABELS } from '@core/domain/gender';
export type { Gender } from '@core/domain/gender';

export interface UserProfile {
  monthlySalary: number | null;
  dailyWage: number | null;
}

export interface User {
  id: string;
  code: string | null;
  fullName: string;
  email: string | null;
  phone: string | null;
  gender: Gender | null;
  age: number | null;
  nid: string;
  role: UserRole;
  status: UserStatus;
  mustChangePassword: boolean;
  profile: UserProfile | null;
}

export interface CreateUserInput {
  fullName: string;
  role: UserRole;
  nid: string;
  email: string | null;
  password: string | null;
  phone: string | null;
  gender: Gender | null;
  age: number | null;
  monthlySalary: number | null;
  dailyWage: number | null;
}

export interface UpdateUserInput extends CreateUserInput {
  status: UserStatus;
}

export function toUser(dto: UserDtoRs): User {
  return {
    id: dto.id,
    code: dto.code,
    fullName: dto.fullName,
    email: dto.email,
    phone: dto.phone,
    gender: dto.gender as Gender | null,
    age: dto.age,
    nid: dto.nid,
    role: dto.role as UserRole,
    status: dto.status as UserStatus,
    mustChangePassword: dto.mustChangePassword,
    profile:
      dto.profile === null
        ? null
        : { monthlySalary: dto.profile.monthlySalary, dailyWage: dto.profile.dailyWage },
  };
}

// Compensation display for the users table: monthly salary (managers) or daily wage
// (moqawel/worker), with an Arabic period label; em dash when there is none.
export function formatCompensation(user: User): string {
  const p = user.profile;
  if (user.role === 'manager' && p?.monthlySalary != null) return `${p.monthlySalary} (راتب شهري)`;
  if ((user.role === 'moqawel' || user.role === 'worker') && p?.dailyWage != null) return `${p.dailyWage} (يومية)`;
  return '—';
}

// The backend rejects profile fields that don't belong to the role, so the
// mappers null out everything the selected role doesn't own (guards against
// stale form values after a role switch).
function gatedProfileFields(role: UserRole, input: CreateUserInput) {
  const hasWage = role === 'moqawel' || role === 'worker';
  return {
    monthlySalary: role === 'manager' ? input.monthlySalary : null,
    dailyWage: hasWage ? input.dailyWage : null,
  };
}

export function toCreateUserDtoRq(input: CreateUserInput): CreateUserDtoRq {
  return {
    fullName: input.fullName,
    role: input.role,
    nid: input.nid,
    email: input.email,
    password: input.password,
    phone: input.phone,
    gender: input.gender,
    age: input.age,
    ...gatedProfileFields(input.role, input),
  };
}

export function toUpdateUserDtoRq(input: UpdateUserInput): UpdateUserDtoRq {
  return {
    fullName: input.fullName,
    role: input.role,
    nid: input.nid,
    status: input.status,
    email: input.email,
    password: input.password,
    phone: input.phone,
    gender: input.gender,
    age: input.age,
    ...gatedProfileFields(input.role, input),
  };
}
