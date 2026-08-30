// update-user.dto.ts — update-user request DTO (API_FLOW convention).
// `role` echoes the user's existing role (backend enforces immutability).
export interface UpdateUserDtoRq {
  fullName: string;
  role: string;
  nid: string;
  status: string;
  email: string | null;
  password: string | null;
  phone: string | null;
  gender: string | null;
  age: number | null;
  monthlySalary: number | null;
  dailyWage: number | null;
}
