import { TestBed } from '@angular/core/testing';
import { CaptainPanelPage } from '@features/captain-panel/presentation/pages/captain-panel/captain-panel.page';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { provideRouter } from '@angular/router';

function build(role: 'head_coach' | 'captain') {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [CaptainPanelPage],
    providers: [provideRouter([]), { provide: AuthSessionStore, useValue: { role: () => role } }],
  });
  return TestBed.createComponent(CaptainPanelPage);
}

describe('CaptainPanelPage', () => {
  it('shows a navigating Medical Tests card for head_coach', () => {
    const fixture = build('head_coach');
    fixture.detectChanges();
    const html = (fixture.nativeElement as HTMLElement).innerHTML;
    expect(html).toContain('/captain-panel/medical-tests');
  });

  it('does not show the Medical Tests card for captain', () => {
    const fixture = build('captain');
    fixture.detectChanges();
    const html = (fixture.nativeElement as HTMLElement).innerHTML;
    expect(html).not.toContain('/captain-panel/medical-tests');
  });
});
