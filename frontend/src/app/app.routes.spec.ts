import { routes } from './app.routes';

describe('routes', () => {
  it('nests home + account + change-password under a guarded shell', () => {
    const shell = routes.find((r) => r.path === '' && !!r.children);
    expect(shell).toBeTruthy();
    expect(shell!.canActivate).toBeTruthy();
    const childPaths = (shell!.children ?? []).map((c) => c.path);
    expect(childPaths).toEqual(expect.arrayContaining(['home', 'account', 'change-password']));
  });

  it('registers blank feature routes under the shell for the sidebar nav items', () => {
    const shell = routes.find((r) => r.path === '' && !!r.children)!;
    const childPaths = (shell.children ?? []).map((c) => c.path);
    expect(childPaths).toEqual(
      expect.arrayContaining(['swimmers', 'attendance', 'championships', 'captain-panel']),
    );
  });

  it('keeps /login outside the shell', () => {
    expect(routes.some((r) => r.path === 'login' && !r.children)).toBe(true);
  });

  it('does not contain a /user-management child (feature parked)', () => {
    const shell = routes.find((r) => r.path === '' && !!r.children)!;
    const um = (shell.children ?? []).find((c) => c.path === 'user-management');
    expect(um).toBeUndefined();
  });

  it('registers the account-creation route', () => {
    const layout = routes.find((r) => r.path === '');
    const paths = layout?.children?.map((c) => c.path) ?? [];
    expect(paths).toContain('captain-panel/account-creation');
  });
});
