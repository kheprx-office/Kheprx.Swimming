import { ROLE_LABELS } from '@core/domain/roles';

describe('ROLE_LABELS', () => {
  it('maps each swimming role to an i18n key', () => {
    expect(ROLE_LABELS.head_coach).toBe('roles.head_coach');
    expect(ROLE_LABELS.captain).toBe('roles.captain');
    expect(Object.keys(ROLE_LABELS).sort()).toEqual(['captain', 'head_coach']);
  });
});
