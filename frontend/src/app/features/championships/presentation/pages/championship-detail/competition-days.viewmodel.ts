import { Injectable, computed, inject, signal } from '@angular/core';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LoadScheduleUseCase } from '@features/championships/domain/usecases/load-schedule.use-case';
import { SaveScheduleUseCase } from '@features/championships/domain/usecases/save-schedule.use-case';
import { LoadEnrollmentsUseCase } from '@features/championships/domain/usecases/load-enrollments.use-case';
import { LoadStrokesUseCase, LoadDistancesUseCase, LookupItem } from '@features/reference';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import {
  ScheduleDay, ScheduleRace, ScheduleDayData,
  serializeSchedule, toScheduleData, raceStatus,
} from '@features/championships/domain/model/competition-schedule';
import { formatDateRange } from '@features/championships/presentation/pages/championships/format-date-range';

@Injectable()
export class CompetitionDaysViewModel {
  private readonly loadScheduleUc = inject(LoadScheduleUseCase);
  private readonly saveScheduleUc = inject(SaveScheduleUseCase);
  private readonly loadEnrollmentsUc = inject(LoadEnrollmentsUseCase);
  private readonly loadStrokesUc = inject(LoadStrokesUseCase);
  private readonly loadDistancesUc = inject(LoadDistancesUseCase);
  private readonly listSwimmersUc = inject(ListSwimmersUseCase);
  private readonly language = inject(LanguageStore);
  private readonly session = inject(AuthSessionStore);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  private eventId = '';
  private keySeq = 0;

  readonly loading = signal(false);
  readonly loaded = signal(false);
  readonly error = signal(false);
  readonly saving = signal(false);

  readonly strokes = signal<LookupItem[]>([]);
  readonly distances = signal<LookupItem[]>([]);
  readonly roster = signal<SwimmerListItem[]>([]);
  private readonly enrolledIds = signal<ReadonlySet<string>>(new Set());
  readonly days = signal<ScheduleDay[]>([]);
  private readonly baseline = signal<string>('[]');

  // Championship date range (YYYY-MM-DD), supplied by the page — bounds the day picker,
  // seeds new days, and caps the number of days at one per calendar date in the range.
  readonly eventStart = signal<string>('');
  readonly eventEnd = signal<string>('');

  readonly canManage = computed(() => {
    const r = this.session.role();
    return r === 'head_coach' || r === 'captain';
  });

  readonly enrolledSwimmers = computed<SwimmerListItem[]>(() => {
    const ids = this.enrolledIds();
    return this.roster().filter((s) => ids.has(s.id));
  });

  readonly dayCount = computed(() => this.days().length);
  readonly raceCount = computed(() => this.days().reduce((a, d) => a + d.races.length, 0));
  readonly entryCount = computed(() =>
    this.days().reduce((a, d) => a + d.races.reduce((b, r) => b + r.swimmerIds.length, 0), 0));
  readonly dirty = computed(() => serializeSchedule(this.days()) !== this.baseline());

  // A day can be added only while some in-range calendar date is still free (one day per date).
  // With an unknown range (dates not supplied) we don't block — the server + picker still guard.
  readonly canAddDay = computed(() => {
    if (!this.canManage()) return false;
    const range = this.rangeDates();
    if (range.length === 0) return true;
    // Cap the total day count at the number of dates in the range. Older data can hold days on
    // out-of-range dates, which would otherwise leave in-range dates "free" and never block.
    if (this.days().length >= range.length) return false;
    const used = new Set(this.days().map((d) => d.dayDate));
    return range.some((dt) => !used.has(dt));
  });

  async ensureLoaded(eventId: string, start = '', end = ''): Promise<void> {
    this.eventStart.set(start);
    this.eventEnd.set(end);
    // Already loaded: don't reload the schedule (would drop unsaved day/race edits), but the
    // enrolled set may have changed on the Enrollment tab — refresh just that so the
    // participant chips stay in sync.
    if (this.loaded() && this.eventId === eventId) {
      await this.syncEnrollment();
      return;
    }
    await this.load(eventId);
  }

  // Re-fetch the roster + enrolled set without touching the (possibly unsaved) schedule tree.
  private async syncEnrollment(): Promise<void> {
    const [rosterRes, enrRes] = await Promise.all([
      this.listSwimmersUc.run(undefined),
      this.loadEnrollmentsUc.run(this.eventId),
    ]);
    if (rosterRes.ok) this.roster.set(rosterRes.data);
    if (enrRes.ok) this.enrolledIds.set(new Set(enrRes.data));
  }

  async load(eventId: string): Promise<void> {
    this.eventId = eventId;
    this.loading.set(true);
    this.error.set(false);
    this.loaded.set(false);

    const [schedRes, strokesRes, distRes, rosterRes, enrRes] = await Promise.all([
      this.loadScheduleUc.run(eventId),
      this.loadStrokesUc.run(),
      this.loadDistancesUc.run(),
      this.listSwimmersUc.run(undefined),
      this.loadEnrollmentsUc.run(eventId),
    ]);

    if (!schedRes.ok) {
      this.loading.set(false);
      this.error.set(true);
      return;
    }
    this.strokes.set(strokesRes.ok ? strokesRes.data : []);
    this.distances.set(distRes.ok ? distRes.data : []);
    this.roster.set(rosterRes.ok ? rosterRes.data : []);
    this.enrolledIds.set(new Set(enrRes.ok ? enrRes.data : []));

    const days = schedRes.data.map((d) => this.hydrateDay(d));
    this.days.set(days);
    this.baseline.set(serializeSchedule(days));
    this.loaded.set(true);
    this.loading.set(false);
  }

  // ---- day ops ----
  // A new day auto-fills the earliest still-free date in the championship range (one day per date),
  // and is a no-op once every in-range date is used (canAddDay gates the button).
  addDay(): void {
    if (!this.canAddDay()) return;
    const n = this.days().length + 1;
    const day: ScheduleDay = {
      key: this.nextKey(),
      labelEn: `${this.i18n.t('championships.days.dayName')} ${n}`,
      labelAr: null,
      dayDate: this.nextFreeDate(),
      races: [],
    };
    this.days.set([...this.days(), day]);
  }

  removeDay(dayKey: string): void {
    if (!this.canManage()) return;
    this.days.set(this.days().filter((d) => d.key !== dayKey));
  }

  updateDay(dayKey: string, patch: Partial<Pick<ScheduleDay, 'labelEn' | 'labelAr' | 'dayDate'>>): void {
    if (!this.canManage()) return;
    this.days.set(this.days().map((d) => (d.key === dayKey ? { ...d, ...patch } : d)));
  }

  // ---- race ops ----
  addRace(dayKey: string): void {
    if (!this.canManage()) return;
    const race: ScheduleRace = {
      key: this.nextKey(),
      strokeId: this.strokes()[0]?.id ?? '',
      distanceId: this.distances()[0]?.id ?? '',
      scheduledTime: null,
      swimmerIds: [],
    };
    this.days.set(this.days().map((d) => (d.key === dayKey ? { ...d, races: [...d.races, race] } : d)));
  }

  removeRace(dayKey: string, raceKey: string): void {
    if (!this.canManage()) return;
    this.days.set(this.days().map((d) =>
      d.key === dayKey ? { ...d, races: d.races.filter((r) => r.key !== raceKey) } : d));
  }

  updateRace(dayKey: string, raceKey: string, patch: Partial<Pick<ScheduleRace, 'strokeId' | 'distanceId' | 'scheduledTime'>>): void {
    if (!this.canManage()) return;
    this.days.set(this.days().map((d) =>
      d.key !== dayKey ? d : { ...d, races: d.races.map((r) => (r.key === raceKey ? { ...r, ...patch } : r)) }));
  }

  toggleSwimmer(dayKey: string, raceKey: string, swimmerId: string): void {
    if (!this.canManage()) return;
    this.days.set(this.days().map((d) => {
      if (d.key !== dayKey) return d;
      return {
        ...d,
        races: d.races.map((r) => {
          if (r.key !== raceKey) return r;
          const has = r.swimmerIds.includes(swimmerId);
          return { ...r, swimmerIds: has ? r.swimmerIds.filter((id) => id !== swimmerId) : [...r.swimmerIds, swimmerId] };
        }),
      };
    }));
  }

  isAssigned(dayKey: string, raceKey: string, swimmerId: string): boolean {
    const day = this.days().find((d) => d.key === dayKey);
    const race = day?.races.find((r) => r.key === raceKey);
    return race?.swimmerIds.includes(swimmerId) ?? false;
  }

  async save(): Promise<void> {
    if (!this.canManage() || this.saving() || !this.dirty()) return;
    this.saving.set(true);
    try {
      const data: ScheduleDayData[] = toScheduleData(this.days());
      const res = await this.saveScheduleUc.run({ eventId: this.eventId, days: data });
      if (res.ok) {
        this.notify.success(this.i18n.t('championships.days.saved'));
        const days = this.adoptSavedTree(res.data);
        this.days.set(days);
        this.baseline.set(serializeSchedule(days));
      } else {
        this.notify.error(this.i18n.t('championships.days.saveFailed'));
      }
    } finally {
      this.saving.set(false);
    }
  }

  // ---- display helpers ----
  strokeLabel(id: string): string { return this.lookupLabel(this.strokes(), id); }
  distanceLabel(id: string): string { return this.lookupLabel(this.distances(), id); }
  swimmerName(s: SwimmerListItem): string {
    return this.language.lang() === 'ar' ? (s.nameAr ?? s.nameEn) : s.nameEn;
  }
  statusOf(day: ScheduleDay, race: ScheduleRace): 'scheduled' | 'awaitingResults' {
    return raceStatus(day.dayDate, race.scheduledTime);
  }
  // Read-only, locale-aware display of a day's (auto-assigned) date; blank for an unset date.
  dayDateLabel(iso: string): string {
    if (!iso) return '';
    return formatDateRange(iso, iso, this.language.lang() === 'ar' ? 'ar' : 'en');
  }

  private lookupLabel(items: LookupItem[], id: string): string {
    const item = items.find((i) => i.id === id);
    if (!item) return '';
    return this.language.lang() === 'ar' ? (item.nameAr ?? item.nameEn) : item.nameEn;
  }

  private adoptSavedTree(data: ScheduleDayData[]): ScheduleDay[] {
    const current = this.days();
    return data.map((d, i) => {
      const existingDay = current[i];
      const dayKey = existingDay?.key ?? this.nextKey();
      return {
        key: dayKey,
        labelEn: d.labelEn,
        labelAr: d.labelAr,
        dayDate: d.dayDate,
        races: d.races.map((r, j) => {
          const existingRace = existingDay?.races[j];
          const raceKey = existingRace?.key ?? this.nextKey();
          return { key: raceKey, ...r, swimmerIds: [...r.swimmerIds] };
        }),
      };
    });
  }

  private hydrateDay(d: ScheduleDayData): ScheduleDay {
    return {
      key: this.nextKey(),
      labelEn: d.labelEn,
      labelAr: d.labelAr,
      dayDate: d.dayDate,
      races: d.races.map((r) => ({ key: this.nextKey(), ...r, swimmerIds: [...r.swimmerIds] })),
    };
  }

  private nextKey(): string { return `k${this.keySeq++}`; }

  // Inclusive list of YYYY-MM-DD dates from eventStart to eventEnd. UTC math keeps it
  // timezone-safe and deterministic; empty/invalid range → [] (treated as "unknown, no cap").
  private rangeDates(): string[] {
    const start = this.eventStart();
    const end = this.eventEnd();
    if (!start || !end) return [];
    let cur = new Date(`${start}T00:00:00Z`);
    const last = new Date(`${end}T00:00:00Z`);
    if (Number.isNaN(cur.getTime()) || Number.isNaN(last.getTime()) || cur > last) return [];
    const out: string[] = [];
    for (let i = 0; cur <= last && i < 400; i++) {
      out.push(cur.toISOString().slice(0, 10));
      cur = new Date(cur.getTime() + 86_400_000);
    }
    return out;
  }

  // Earliest in-range date not already used by a day; falls back to eventStart (canAddDay gates use).
  private nextFreeDate(): string {
    const used = new Set(this.days().map((d) => d.dayDate));
    return this.rangeDates().find((dt) => !used.has(dt)) ?? this.eventStart();
  }
}
