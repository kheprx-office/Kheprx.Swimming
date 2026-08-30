// session.dto.ts — session response DTOs (API_FLOW convention).
// SessionDtoRs fields are raw/untrusted transport — isSessionDtoRsValid guards
// them before the use cases map to domain models.
import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { ROLE_LABELS } from '@core/domain/roles';

export interface SessionDtoRs {
  accessToken: string;
  refreshToken: string;
  role: string;
  userId: string;
  mustChangePassword: boolean;
}

export interface SessionItemDtoRs extends BaseResponseRs<SessionDtoRs> {}

export function isSessionDtoRsValid(dto: SessionDtoRs): boolean {
  return (
    typeof dto.accessToken === 'string' && dto.accessToken.length > 0 &&
    typeof dto.refreshToken === 'string' && dto.refreshToken.length > 0 &&
    typeof dto.userId === 'string' && dto.userId.length > 0 &&
    typeof dto.role === 'string' &&
    Object.prototype.hasOwnProperty.call(ROLE_LABELS, dto.role) &&
    typeof dto.mustChangePassword === 'boolean'
  );
}
