import { Injectable, computed, inject, signal } from '@angular/core';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LoadChampionshipUseCase } from '@features/championships/domain/usecases/load-championship.use-case';
import { LoadEnrollmentsUseCase } from '@features/championships/domain/usecases/load-enrollments.use-case';
import { SaveEnrollmentsUseCase } from '@features/championships/domain/usecases/save-enrollments.use-case';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import { Championship } from '@features/championships/domain/model/championship';
import { formatDateRange } from '@features/championships/presentation/pages/championships/format-date-range';

export type DetailTab = 'enrollment' | 'days' | 'finished' | 'results';

@Injectable()
export class ChampionshipDetailViewModel {
  private readonly loadChampionshipUc = inject(LoadChampionshipUseCase);
  private readonly loadEnrollmentsUc = inject(LoadEnrollmentsUseCase);
  private readonly saveEnrollmentsUc = inject(SaveEnrollmentsUseCase);
  private readonly listSwimmersUc = inject(ListSwimmersUseCase);
  private readonly language = inject(LanguageStore);
  private readonly session = inject(AuthSessionStore);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  private eventId = '';

  readonly loading = signal(true);
  readonly error = signal(false);
  readonly notFound = signal(false);
  readonly saving = signal(false);
  readonly enrollmentsLoaded = signal(false);
  readonly championship = signal<Championship | null>(null);
  readonly roster = signal<SwimmerListItem[]>([]);
  readonly search = signal('');
  readonly activeTab = signal<DetailTab>('enrollment');

  // Enrolled set the server returned (baseline) vs the user's working edits.
  private readonly baseline = signal<ReadonlySet<string>>(new Set());
  private readonly working = signal<ReadonlySet<string>>(new Set());

  readonly canManage = computed(() => {
    const r = this.session.role();
    return r === 'head_coach' || r === 'captain';
  });

  readonly total = computed(() => this.roster().length);
  readonly enrolledCount = computed(() => this.working().size);

  readonly dirty = computed(() => {
    const a = this.baseline();
    const b = this.working();
    if (a.size !== b.size) return true;
    for (const id of b) if (!a.has(id)) return true;
    return false;
  });

  readonly filtered = computed<SwimmerListItem[]>(() => {
    const q = this.search().trim().toLowerCase();
    if (!q) return this.roster();
    return this.roster().filter(
      (s) => s.nameEn.toLowerCase().includes(q) || (s.nameAr ?? '').toLowerCase().includes(q),
    );
  });

  async load(id: string): Promise<void> {
    this.eventId = id;
    this.activeTab.set('enrollment');
    this.search.set('');
    this.loading.set(true);
    this.error.set(false);
    this.notFound.set(false);
    this.enrollmentsLoaded.set(false);

    const champRes = await this.loadChampionshipUc.run(id);
    if (!champRes.ok) {
      this.loading.set(false);
      this.championship.set(null);
      if (champRes.error.status === 404) this.notFound.set(true);
      else this.error.set(true);
      return;
    }
    this.championship.set(champRes.data);

    const [rosterRes, enrRes] = await Promise.all([
      this.listSwimmersUc.run(undefined),
      this.loadEnrollmentsUc.run(id),
    ]);
    this.roster.set(rosterRes.ok ? rosterRes.data : []);
    if (enrRes.ok) {
      const enrolled = new Set(enrRes.data);
      this.baseline.set(new Set(enrolled));
      this.working.set(new Set(enrolled));
      this.enrollmentsLoaded.set(true);
    } else {
      this.enrollmentsLoaded.set(false);
      this.error.set(true);
    }
    if (!rosterRes.ok) this.error.set(true);
    this.loading.set(false);
  }

  setTab(tab: DetailTab): void { this.activeTab.set(tab); }
  setSearch(v: string): void { this.search.set(v); }
  isEnrolled(swimmerId: string): boolean { return this.working().has(swimmerId); }

  toggle(swimmerId: string): void {
    if (!this.canManage()) return;
    const next = new Set(this.working());
    if (next.has(swimmerId)) next.delete(swimmerId);
    else next.add(swimmerId);
    this.working.set(next);
  }

  async save(): Promise<void> {
    if (!this.canManage() || this.saving() || !this.dirty() || !this.enrollmentsLoaded()) return;
    this.saving.set(true);
    try {
      const swimmerIds = Array.from(this.working());
      const res = await this.saveEnrollmentsUc.run({ eventId: this.eventId, swimmerIds });
      if (res.ok) {
        this.notify.success(this.i18n.t('championships.enrollment.saved'));
        this.baseline.set(new Set(res.data));
        this.working.set(new Set(res.data));
      } else {
        this.notify.error(this.i18n.t('championships.enrollment.saveFailed'));
      }
    } finally {
      this.saving.set(false);
    }
  }

  name(): string {
    const c = this.championship();
    if (!c) return '';
    return this.language.lang() === 'ar' ? (c.nameAr ?? c.nameEn) : c.nameEn;
  }
  location(): string {
    const c = this.championship();
    if (!c) return '';
    return (this.language.lang() === 'ar' ? (c.locationAr ?? c.locationEn) : c.locationEn) ?? '';
  }
  dateRange(): string {
    const c = this.championship();
    if (!c) return '';
    return formatDateRange(c.startDate, c.endDate, this.language.lang() === 'ar' ? 'ar' : 'en');
  }
  swimmerName(s: SwimmerListItem): string {
    return this.language.lang() === 'ar' ? (s.nameAr ?? s.nameEn) : s.nameEn;
  }
  club(s: SwimmerListItem): string {
    return (this.language.lang() === 'ar' ? (s.clubNameAr ?? s.clubNameEn) : s.clubNameEn) ?? '';
  }
  initials(s: SwimmerListItem): string {
    return this.swimmerName(s).split(' ').map((p) => p[0]).join('').slice(0, 2).toUpperCase();
  }
}
