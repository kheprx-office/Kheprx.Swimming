import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';
import { Gender } from '@core/domain/gender/gender';
import { GENDER_LABELS } from '@core/domain/gender/gender-labels';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';

type GenderFilter = 'all' | Gender;

// Swimmers roster: server-side search (debounced re-fetch) + client-side gender filter.
// Attendance is intentionally absent — the backend has no attendance data (see spec).
@Component({
  selector: 'app-swimmers-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './swimmers.page.html',
})
export class SwimmersPage implements OnInit, OnDestroy {
  private readonly listSwimmers = inject(ListSwimmersUseCase);
  private readonly language = inject(LanguageStore);

  readonly loading = signal(true);
  readonly error = signal(false);
  readonly search = signal('');
  readonly genderFilter = signal<GenderFilter>('all');
  private readonly swimmers = signal<SwimmerListItem[]>([]);

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  readonly visible = computed<SwimmerListItem[]>(() => {
    const g = this.genderFilter();
    const list = this.swimmers();
    return g === 'all' ? list : list.filter((s) => s.gender === g);
  });

  ngOnInit(): void { void this.load(); }

  ngOnDestroy(): void { if (this.searchTimer) clearTimeout(this.searchTimer); }

  onSearchInput(value: string): void {
    this.search.set(value);
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => void this.load(), 300);
  }

  setGenderFilter(value: string): void {
    this.genderFilter.set(value === 'male' || value === 'female' ? value : 'all');
  }

  displayName(s: SwimmerListItem): string {
    return this.language.lang() === 'ar' ? (s.nameAr ?? s.nameEn) : s.nameEn;
  }

  clubName(s: SwimmerListItem): string {
    const ar = this.language.lang() === 'ar';
    return (ar ? s.clubNameAr ?? s.clubNameEn : s.clubNameEn) ?? '';
  }

  genderLabelKey(s: SwimmerListItem): string {
    return s.gender ? GENDER_LABELS[s.gender] : '';
  }

  initials(s: SwimmerListItem): string {
    const parts = s.nameEn.trim().split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const term = this.search().trim();
    const result = await this.listSwimmers.run(term ? term : undefined);
    if (result.ok) {
      this.swimmers.set(result.data);
    } else {
      this.error.set(true);
      this.swimmers.set([]);
    }
    this.loading.set(false);
  }
}
