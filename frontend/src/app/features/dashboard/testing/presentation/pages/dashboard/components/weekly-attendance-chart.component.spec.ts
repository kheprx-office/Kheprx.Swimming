import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { WeeklyAttendanceChartComponent } from '@features/dashboard/presentation/pages/dashboard/components/weekly-attendance-chart.component';
import { LanguageStore } from '@core/i18n/language.store';

function build(lang: 'en' | 'ar' = 'en') {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [WeeklyAttendanceChartComponent],
    providers: [{ provide: LanguageStore, useValue: { lang: signal(lang) } }],
  });
  const fixture = TestBed.createComponent(WeeklyAttendanceChartComponent);
  return { fixture, cmp: fixture.componentInstance };
}

const DAYS = [
  { date: '2026-09-21', present: 8, absent: 2 },
  { date: '2026-09-22', present: 6, absent: 4 },
  { date: '2026-09-23', present: 10, absent: 0 },
];

describe('WeeklyAttendanceChartComponent', () => {
  it('hasData reflects whether any days are present', () => {
    const { fixture, cmp } = build();
    expect(cmp.hasData()).toBe(false);
    fixture.componentRef.setInput('days', DAYS);
    expect(cmp.hasData()).toBe(true);
  });

  it('maxVal is the largest present/absent value, floored at 1', () => {
    const { fixture, cmp } = build();
    expect(cmp.maxVal()).toBe(1);
    fixture.componentRef.setInput('days', DAYS);
    expect(cmp.maxVal()).toBe(10);
  });

  it('yFor maps 0 to the plot bottom and maxVal to the plot top', () => {
    const { fixture, cmp } = build();
    fixture.componentRef.setInput('days', DAYS);
    expect(cmp.yFor(cmp.maxVal())).toBeCloseTo(cmp.padTop, 5);
    expect(cmp.yFor(0)).toBeCloseTo(cmp.vbHeight - cmp.padBottom, 5);
  });

  it('builds one point per day, a line that starts with a move, and a closed area', () => {
    const { fixture, cmp } = build();
    fixture.componentRef.setInput('days', DAYS);
    expect(cmp.presentPts().length).toBe(3);
    expect(cmp.presentLine().startsWith('M ')).toBe(true);
    expect(cmp.presentArea().trim().endsWith('Z')).toBe(true);
  });

  it('reverses the plot order in RTL so the newest day plots last-to-first', () => {
    const { fixture, cmp } = build('ar');
    fixture.componentRef.setInput('days', DAYS);
    expect(cmp.plotDays()[0].date).toBe('2026-09-23');
  });
});
