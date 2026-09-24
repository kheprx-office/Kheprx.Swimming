import { ROLE_LABELS } from '@core/domain/roles';

describe('ROLE_LABELS', () => {
  it('maps each role to its i18n key', () => {
    expect(ROLE_LABELS.head_coach).toBe('roles.head_coach');
    expect(ROLE_LABELS.captain).toBe('roles.captain');
    expect(ROLE_LABELS.swimmer).toBe('roles.swimmer');
    expect(Object.keys(ROLE_LABELS).sort()).toEqual(['captain', 'head_coach', 'swimmer']);
  });
});
