// role-option.ts — a selectable role on the login screen (domain model).
import { RoleDtoRs } from '@features/auth/data/dto/roles/role.dto';

export interface RoleOption {
  code: string;
  nameEn: string;
  nameAr: string | null;
}

export function toRoleOption(dto: RoleDtoRs): RoleOption {
  return { code: dto.code, nameEn: dto.nameEn, nameAr: dto.nameAr };
}
