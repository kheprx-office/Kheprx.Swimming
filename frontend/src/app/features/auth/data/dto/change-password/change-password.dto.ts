// change-password.dto.ts — change-password request DTO (API_FLOW convention).
export interface ChangePasswordDtoRq {
  currentPassword: string;
  newPassword: string;
}
