import { Component, computed, inject, input } from '@angular/core';
import { TranslatePipe } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';
import { StrokeSplitItem } from '@features/dashboard/domain/model/dashboard-summary';

// Harmonious palette: teal ramp (deep→light) + coral + gold. Kept local; not a global token.
const SEGMENT_COLORS = ['#0A6577', '#33B0C4', '#6BCEDB', '#C33F16', '#FBBF24'];

/**
 * Swimmers-by-stroke — a segmented share bar + colored-dot list. The header shows
 * the real registered-swimmer count; segments are each stroke's share of the summed
 * stroke counts (swimmers may specialize in several strokes). The insight line is
 * shown only when there is a single non-zero leader, so it never states a falsehood.
 */
@Component({
  selector: 'app-swimmers-by-stroke',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './swimmers-by-stroke.component.html',
})
export class SwimmersByStrokeComponent {
  private readonly language = inject(LanguageStore);
  readonly strokes = input<StrokeSplitItem[]>([]);
  readonly swimmerCount = input<number>(0);

  readonly hasData = computed(() => this.strokes().length > 0);
  readonly totalStrokeCount = computed(() => this.strokes().reduce((a, s) => a + s.count, 0));

  segmentPct(count: number): number {
    const total = this.totalStrokeCount();
    return total > 0 ? (count / total) * 100 : 0;
  }

  colorFor(index: number): string {
    return SEGMENT_COLORS[index % SEGMENT_COLORS.length];
  }

  strokeName(s: StrokeSplitItem): string {
    return this.language.lang() === 'ar' ? (s.nameAr ?? s.nameEn) : s.nameEn;
  }

  // The single leading stroke, or null on a tie / all-zero — so the insight never lies.
  readonly topStroke = computed<StrokeSplitItem | null>(() => {
    const s = this.strokes();
    if (s.length === 0) return null;
    const max = Math.max(...s.map((x) => x.count));
    if (max <= 0) return null;
    const leaders = s.filter((x) => x.count === max);
    return leaders.length === 1 ? leaders[0] : null;
  });
}
