import { TestBed } from '@angular/core/testing';
import { AccountCreationPage } from '@features/captain-panel';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

type PageProbe = {
  tab: (() => 'swimmer' | 'captain') & { set: (v: 'swimmer' | 'captain') => void };
  canRegisterCaptain: () => boolean;
};

function setup(role: 'head_coach' | 'captain'): PageProbe {
  const auth = { role: () => role } as unknown as AuthSessionStore;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [AccountCreationPage],
    providers: [{ provide: AuthSessionStore, useValue: auth }],
  });
  return TestBed.createComponent(AccountCreationPage).componentInstance as unknown as PageProbe;
}

describe('AccountCreationPage', () => {
  it('defaults to the swimmer tab and switches to captain', () => {
    const cmp = setup('head_coach');
    expect(cmp.tab()).toBe('swimmer');
    cmp.tab.set('captain');
    expect(cmp.tab()).toBe('captain');
  });

  it('shows the Register Captain tab only for head_coach', () => {
    expect(setup('head_coach').canRegisterCaptain()).toBe(true);
    expect(setup('captain').canRegisterCaptain()).toBe(false);
  });
});
