import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LayoutComponent } from './layout.component';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

function setup(role: 'admin' | 'manager' | 'moqawel' | 'worker') {
  const signOut = jest.fn().mockResolvedValue(undefined);
  const auth = {
    role: () => role,
    currentUserName: () => 'Sara Ali',
    signOut,
  } as unknown as AuthSessionStore;
  TestBed.configureTestingModule({
    imports: [LayoutComponent],
    providers: [provideRouter([]), { provide: AuthSessionStore, useValue: auth }],
  });
  const fixture = TestBed.createComponent(LayoutComponent);
  fixture.detectChanges();
  return { fixture, signOut };
}

describe('LayoutComponent', () => {
  it('shows إدارة المستخدمين for an admin', () => {
    const { fixture } = setup('admin');
    expect(fixture.nativeElement.textContent).toContain('إدارة المستخدمين');
  });

  it('hides إدارة المستخدمين for a non-admin', () => {
    const { fixture } = setup('worker');
    expect(fixture.nativeElement.textContent).not.toContain('إدارة المستخدمين');
  });

  it('renders the avatar initial from the user name', () => {
    const { fixture } = setup('admin');
    const avatar = fixture.nativeElement.querySelector('.rounded-full');
    expect(avatar?.textContent?.trim()).toBe('S');
  });

  it('signs out via the store', async () => {
    const { fixture, signOut } = setup('admin');
    await fixture.componentInstance.signOut();
    expect(signOut).toHaveBeenCalled();
  });

  it('gives an admin الرئيسية, إدارة المستخدمين and الإعدادات', () => {
    const { fixture } = setup('admin');
    const labels = Array.from(fixture.nativeElement.querySelectorAll('nav button')).map((b) =>
      (b as HTMLElement).textContent?.trim(),
    );
    expect(labels).toEqual(['الرئيسية', 'إدارة المستخدمين', 'الإعدادات']);
  });

  it('gives a non-admin worker only الرئيسية and الإعدادات', () => {
    const { fixture } = setup('worker');
    const labels = Array.from(fixture.nativeElement.querySelectorAll('nav button')).map((b) =>
      (b as HTMLElement).textContent?.trim(),
    );
    expect(labels).toEqual(['الرئيسية', 'الإعدادات']);
  });
});
