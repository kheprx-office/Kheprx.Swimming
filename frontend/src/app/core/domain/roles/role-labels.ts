import { UserRole } from '@core/domain/roles/user-role';

export const ROLE_LABELS: Record<UserRole, string> = {
  admin: 'ادمن',
  manager: 'مدير مشروع',
  moqawel: 'مقاول',
  worker: 'عامل',
};
