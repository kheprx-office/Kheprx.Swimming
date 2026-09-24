import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { SwimmersByStrokeComponent } from '@features/dashboard/presentation/pages/dashboard/components/swimmers-by-stroke.component';
import { LanguageStore } from '@core/i18n/language.store';

function build(lang: 'en' | 'ar' = 'en') {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [SwimmersByStrokeComponent],
    providers: [{ provide: LanguageStore, useValue: { lang: signal(lang) } }],
  });
  const fixture = TestBed.createComponent(SwimmersByStrokeComponent);
  return { fixture, cmp: fixture.componentInstance };
}

const S = (n: number, c: number, code: string) => ({ strokeId: 's' + n, code, nameEn: code, nameAr: null, count: c });

describe('SwimmersByStrokeComponent', () => {
  it('totalStrokeCount sums counts and segmentPct is share of that total', () => {
    const { fixture, cmp } = build();
    fixture.componentRef.setInput('strokes', [S(1, 3, 'free'), S(2, 1, 'back')]);
    expect(cmp.totalStrokeCount()).toBe(4);
    expect(cmp.segmentPct(3)).toBe(75);
    expect(cmp.segmentPct(1)).toBe(25);
  });

  it('segmentPct is 0 when there are no strokes (no divide-by-zero)', () => {
    const { cmp } = build();
    expect(cmp.segmentPct(5)).toBe(0);
  });

  it('colorFor cycles through the palette', () => {
    const { cmp } = build();
    expect(cmp.colorFor(0)).toBe(cmp.colorFor(5));
  });

  it('topStroke is the unique leader, or null on a tie or all-zero', () => {
    const { fixture, cmp } = build();
    fixture.componentRef.setInput('strokes', [S(1, 9, 'free'), S(2, 3, 'back')]);
    expect(cmp.topStroke()?.code).toBe('free');
    fixture.componentRef.setInput('strokes', [S(1, 5, 'free'), S(2, 5, 'back')]);
    expect(cmp.topStroke()).toBeNull();
    fixture.componentRef.setInput('strokes', [S(1, 0, 'free'), S(2, 0, 'back')]);
    expect(cmp.topStroke()).toBeNull();
  });

  it('strokeName uses the Arabic name when the language is ar', () => {
    const { cmp } = build('ar');
    expect(cmp.strokeName({ strokeId: 's1', code: 'free', nameEn: 'Freestyle', nameAr: 'حرة', count: 3 })).toBe('حرة');
  });
});
