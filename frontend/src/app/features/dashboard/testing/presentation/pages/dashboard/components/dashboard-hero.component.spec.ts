import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { DashboardHeroComponent } from '@features/dashboard/presentation/pages/dashboard/components/dashboard-hero.component';
import { LanguageStore } from '@core/i18n/language.store';

function build() {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [DashboardHeroComponent],
    providers: [{ provide: LanguageStore, useValue: { lang: signal('en') } }],
  });
  const fixture = TestBed.createComponent(DashboardHeroComponent);
  return { fixture, cmp: fixture.componentInstance };
}

describe('DashboardHeroComponent', () => {
  it('greetingKeyFor maps the hour to the right time-of-day key', () => {
    const { cmp } = build();
    expect(cmp.greetingKeyFor(8)).toBe('dashboard.goodMorning');
    expect(cmp.greetingKeyFor(13)).toBe('dashboard.goodAfternoon');
    expect(cmp.greetingKeyFor(20)).toBe('dashboard.goodEvening');
  });

  it('ringDashArray fills proportionally and treats null as 0', () => {
    const { cmp } = build();
    expect(cmp.ringDashArray(50)).toBe('163.5 327');
    expect(cmp.ringDashArray(null)).toBe('0 327');
  });

  it('showGrowth is true only when newThisMonth > 0', () => {
    const { fixture, cmp } = build();
    fixture.componentRef.setInput('newThisMonth', 0);
    expect(cmp.showGrowth()).toBe(false);
    fixture.componentRef.setInput('newThisMonth', 3);
    expect(cmp.showGrowth()).toBe(true);
  });

  it('renders the greeting, user name and rate without error', () => {
    const { fixture } = build();
    fixture.componentRef.setInput('userName', 'John');
    fixture.componentRef.setInput('swimmerCount', 452);
    fixture.componentRef.setInput('attendanceRatePct', 88);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('John');
    expect(text).toContain('452');
    expect(text).toContain('88%');
  });

  it('shows an em dash when the attendance rate is null', () => {
    const { fixture } = build();
    fixture.componentRef.setInput('attendanceRatePct', null);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('—');
  });
});
