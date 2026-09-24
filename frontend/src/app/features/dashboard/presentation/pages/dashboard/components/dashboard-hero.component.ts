import { Component, computed, inject, input } from '@angular/core';
import { LucideDynamicIcon, LucideTrendingUp, LucideWaves } from '@lucide/angular';
import { TranslatePipe } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';

/**
 * Dashboard hero band — a dark-teal `.ocean-panel` with the greeting, swimmer
 * count + growth, and the large attendance donut. Pure presentational: all data
 * arrives via inputs; only the language (for the localized date) is read.
 */
@Component({
  selector: 'app-dashboard-hero',
  standalone: true,
  imports: [TranslatePipe, LucideDynamicIcon],
  templateUrl: './dashboard-hero.component.html',
})
export class DashboardHeroComponent {
  private readonly language = inject(LanguageStore);

  readonly userName = input<string>('');
  readonly swimmerCount = input<number>(0);
  readonly newThisMonth = input<number>(0);
  readonly attendanceRatePct = input<number | null>(null);

  readonly TrendingUpIcon = LucideTrendingUp;
  readonly WavesIcon = LucideWaves;

  // Donut geometry: r=52 → circumference ≈ 327 (matches the app's existing ring).
  readonly ringCircumference = 327;

  readonly showGrowth = computed(() => this.newThisMonth() > 0);

  // Split out as a pure function so the time-of-day branch is deterministically testable.
  greetingKeyFor(hour: number): string {
    if (hour < 12) return 'dashboard.goodMorning';
    if (hour < 18) return 'dashboard.goodAfternoon';
    return 'dashboard.goodEvening';
  }
  readonly greetingKey = computed(() => this.greetingKeyFor(new Date().getHours()));

  // Localized "Thursday, 24 September" date badge.
  readonly todayLabel = computed(() => {
    const locale = this.language.lang() === 'ar' ? 'ar-EG' : 'en-US';
    try {
      return new Intl.DateTimeFormat(locale, { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date());
    } catch {
      return '';
    }
  });

  ringDashArray(pct: number | null): string {
    const filled = ((pct ?? 0) / 100) * this.ringCircumference;
    return `${filled} ${this.ringCircumference}`;
  }
}
