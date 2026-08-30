import { routes } from './app.routes';

describe('routes', () => {
  it('nests home + account + change-password under a guarded shell', () => {
    const shell = routes.find((r) => r.path === '' && !!r.children);
    expect(shell).toBeTruthy();
    expect(shell!.canActivate).toBeTruthy();
    const childPaths = (shell!.children ?? []).map((c) => c.path);
    expect(childPaths).toEqual(expect.arrayContaining(['home', 'account', 'change-password']));
  });

  it('keeps /login outside the shell', () => {
    expect(routes.some((r) => r.path === 'login' && !r.children)).toBe(true);
  });

  it('adds a guarded /user-management child', () => {
    const shell = routes.find((r) => r.path === '' && !!r.children)!;
    const um = (shell.children ?? []).find((c) => c.path === 'user-management');
    expect(um).toBeTruthy();
    expect(um!.canActivate).toBeTruthy();
    expect(um!.providers).toBeTruthy();
  });
});
