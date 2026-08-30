// create-user.dto.ts — create-user request DTO (API_FLOW convention).
// Flat shape matching the backend CreateUserRequest; profile fields for the
// wrong role must be null (the backend rejects them, not ignores them).
export interface CreateUserDtoRq {
  fullName: string;
  role: string;
  nid: string;
  email: string | null;
  password: string | null;
  phone: string | null;
  gender: string | null;
  age: number | null;
  monthlySalary: number | null;
  dailyWage: number | null;
}
