import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { AUTH_REPOSITORY } from '@features/auth/domain/repositories/auth.repository';
import { RoleOption, toRoleOption } from '@features/auth/domain/model/roles/role-option';
import { isRoleDtoRsValid } from '@features/auth/data/dto/roles/role.dto';

// LoadRolesUseCase: fetches the selectable roles (GET /api/roles) for the login
// screen's role selector. The endpoint is anonymous so it can run before sign-in.
@Injectable({ providedIn: 'root' })
export class LoadRolesUseCase extends UseCase<void, RoleOption[]> {
  private readonly repo = inject(AUTH_REPOSITORY);
  constructor() { super('LoadRoles'); }
  protected async execute(): Promise<RoleOption[]> {
    const res = await this.repo.getRoles();
    const rows = res.data ?? [];
    if (!Array.isArray(rows) || !rows.every(isRoleDtoRsValid)) {
      throw new AppError('Invalid roles data received', 'validation');
    }
    return rows.map(toRoleOption);
  }
}
