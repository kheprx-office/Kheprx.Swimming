import { Component, computed, inject, input } from '@angular/core';
import { TranslatePipe } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';
import { DailyAttendance } from '@features/dashboard/domain/model/dashboard-summary';

interface Pt {
  x: number;
  y: number;
}

/**
 * Weekly attendance — a lightweight inline-SVG area/line chart (no chart library).
 * Present is a teal filled area, Absent a coral line, over the last recorded days.
 * All geometry is pure and testable; nothing is fetched here.
 */
@Component({
  selector: 'app-weekly-attendance-chart',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './weekly-attendance-chart.component.html',
})
export class WeeklyAttendanceChartComponent {
  private readonly language = inject(LanguageStore);
  readonly days = input<DailyAttendance[]>([]);

  // viewBox geometry (the SVG scales to its container via width:100%).
  readonly vbWidth = 700;
  readonly vbHeight = 240;
  readonly padX = 24;
  readonly padTop = 16;
  readonly padBottom = 34;

  readonly hasData = computed(() => this.days().length > 0);

  // Reversed in RTL so the most recent day sits at the inline-end.
  readonly plotDays = computed<DailyAttendance[]>(() => {
    const d = this.days();
    return this.language.lang() === 'ar' ? [...d].reverse() : d;
  });

  // Largest present/absent value, floored at 1 so the y-scale never divides by zero.
  readonly maxVal = computed(() => Math.max(1, ...this.days().map((x) => Math.max(x.present, x.absent))));

  xFor(i: number, n: number): number {
    if (n <= 1) return this.vbWidth / 2;
    const usable = this.vbWidth - this.padX * 2;
    return this.padX + (usable * i) / (n - 1);
  }

  yFor(v: number): number {
    const usable = this.vbHeight - this.padTop - this.padBottom;
    return this.padTop + usable * (1 - v / this.maxVal());
  }

  private points(sel: (d: DailyAttendance) => number): Pt[] {
    const d = this.plotDays();
    return d.map((day, i) => ({ x: this.xFor(i, d.length), y: this.yFor(sel(day)) }));
  }
  readonly presentPts = computed(() => this.points((d) => d.present));
  readonly absentPts = computed(() => this.points((d) => d.absent));

  // Smooth line via Catmull-Rom → cubic bezier (falls back to a move/line for 0–1 points).
  linePath(pts: Pt[]): string {
    if (pts.length === 0) return '';
    if (pts.length === 1) return `M ${pts[0].x.toFixed(2)} ${pts[0].y.toFixed(2)}`;
    let d = `M ${pts[0].x.toFixed(2)} ${pts[0].y.toFixed(2)}`;
    for (let i = 0; i < pts.length - 1; i++) {
      const p0 = pts[i - 1] ?? pts[i];
      const p1 = pts[i];
      const p2 = pts[i + 1];
      const p3 = pts[i + 2] ?? p2;
      const c1x = p1.x + (p2.x - p0.x) / 6;
      const c1y = p1.y + (p2.y - p0.y) / 6;
      const c2x = p2.x - (p3.x - p1.x) / 6;
      const c2y = p2.y - (p3.y - p1.y) / 6;
      d += ` C ${c1x.toFixed(2)} ${c1y.toFixed(2)}, ${c2x.toFixed(2)} ${c2y.toFixed(2)}, ${p2.x.toFixed(2)} ${p2.y.toFixed(2)}`;
    }
    return d;
  }

  readonly presentLine = computed(() => this.linePath(this.presentPts()));
  readonly absentLine = computed(() => this.linePath(this.absentPts()));

  readonly presentArea = computed(() => {
    const pts = this.presentPts();
    if (pts.length === 0) return '';
    const baseline = this.vbHeight - this.padBottom;
    const last = pts[pts.length - 1];
    const first = pts[0];
    return `${this.linePath(pts)} L ${last.x.toFixed(2)} ${baseline} L ${first.x.toFixed(2)} ${baseline} Z`;
  });

  // Horizontal grid lines at 0 / 50 / 100 % of the max.
  readonly gridYs = computed(() => [0, 0.5, 1].map((f) => this.yFor(this.maxVal() * f)));

  weekdayLabel(dateIso: string): string {
    const locale = this.language.lang() === 'ar' ? 'ar-EG' : 'en-US';
    try {
      return new Intl.DateTimeFormat(locale, { weekday: 'short' }).format(new Date(dateIso + 'T00:00:00'));
    } catch {
      return dateIso;
    }
  }
}
