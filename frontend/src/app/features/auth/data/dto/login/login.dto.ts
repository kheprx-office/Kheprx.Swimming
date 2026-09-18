// login.dto.ts — login request DTO (API_FLOW convention).
export interface LoginDtoRq {
  email: string;
  password: string;
  role: string;
}
