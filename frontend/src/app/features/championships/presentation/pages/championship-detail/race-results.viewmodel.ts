import { Injectable, computed, inject, signal } from '@angular/core';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LoadRaceScheduleUseCase } from '@features/championships/domain/usecases/load-race-schedule.use-case';
import { LoadResultsUseCase } from '@features/championships/domain/usecases/load-results.use-case';
import { SaveRaceResultsUseCase } from '@features/championships/domain/usecases/save-race-results.use-case';
import { LoadStrokesUseCase, LoadDistancesUseCase, LookupItem } from '@features/reference';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import {
  RaceScheduleDay, RaceResultEntry, FinishedRaceCard, ResultsRaceCard,
} from '@features/championships/domain/model/race-result';
import { formatMsToTime, parseTimeToMs } from '@features/championships/domain/model/race-time';

@Injectable()
export class RaceResultsViewModel {
  private readonly loadScheduleUc = inject(LoadRaceScheduleUseCase);
  private readonly loadResultsUc = inject(LoadResultsUseCase);
  private readonly saveResultsUc = inject(SaveRaceResultsUseCase);
  private readonly loadStrokesUc = inject(LoadStrokesUseCase);
  private readonly loadDistancesUc = inject(LoadDistancesUseCase);
  private readonly listSwimmersUc = inject(ListSwimmersUseCase);
  private readonly language = inject(LanguageStore);
  private readonly session = inject(AuthSessionStore);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  private eventId = '';

  readonly loading = signal(false);
  readonly loaded = signal(false);
  readonly error = signal(false);
  readonly saving = signal(false);

  private readonly strokes = signal<LookupItem[]>([]);
  private readonly distances = signal<LookupItem[]>([]);
  private readonly roster = signal<SwimmerListItem[]>([]);
  private readonly schedule = signal<RaceScheduleDay[]>([]);
  private readonly results = signal<RaceResultEntry[]>([]);

  readonly openRaceId = signal<string | null>(null);
  // sessionId -> swimmerId -> raw text input
  private readonly inputs = signal<Record<string, Record<string, string>>>({});

  readonly canManage = computed(() => {
    const r = this.session.role();
    return r === 'head_coach' || r === 'captain';
  });

  private readonly resultsBySession = computed(() => {
    const map = new Map<string, RaceResultEntry[]>();
    for (const r of this.results()) {
      const list = map.get(r.raceSessionId) ?? [];
      list.push(r);
      map.set(r.raceSessionId, list);
    }
    return map;
  });

  readonly finishedRaces = computed<FinishedRaceCard[]>(() => {
    const done = this.resultsBySession();
    const cards: FinishedRaceCard[] = [];
    for (const day of this.schedule()) {
      for (const race of day.races) {
        // Every race without results yet is listed, regardless of its scheduled time;
        // a race leaves this list once its times are entered (moves to the Results tab).
        if (done.has(race.id)) continue;
        cards.push({
          raceSessionId: race.id,
          raceName: this.raceName(race.distanceId, race.strokeId),
          dayLabel: this.dayLabel(day),
          scheduledTime: race.scheduledTime,
          swimmers: race.swimmerIds
            .map((id) => this.roster().find((s) => s.id === id))
            .filter((s): s is SwimmerListItem => !!s)
            .map((s) => ({ id: s.id, name: this.swimmerName(s) })),
        });
      }
    }
    return cards;
  });

  readonly finishedCount = computed(() => this.finishedRaces().length);

  readonly resultRaces = computed<ResultsRaceCard[]>(() => {
    const done = this.resultsBySession();
    const cards: ResultsRaceCard[] = [];
    for (const day of this.schedule()) {
      for (const race of day.races) {
        const rows = done.get(race.id);
        if (!rows || rows.length === 0) continue;
        const entries = [...rows]
          .sort((a, b) => a.timeMs - b.timeMs)
          .map((r, i) => ({
            swimmerName: this.swimmerNameById(r.swimmerId),
            timeMs: r.timeMs,
            rank: i + 1,
            isPersonalBest: r.isPersonalBest,
          }));
        cards.push({ raceSessionId: race.id, raceName: this.raceName(race.distanceId, race.strokeId), dayLabel: this.dayLabel(day), entries });
      }
    }
    return cards;
  });

  async ensureLoaded(eventId: string): Promise<void> {
    if (this.loaded() && this.eventId === eventId) return;
    await this.load(eventId);
  }

  async load(eventId: string): Promise<void> {
    this.eventId = eventId;
    this.loading.set(true);
    this.error.set(false);
    this.loaded.set(false);
    this.openRaceId.set(null);
    this.inputs.set({});

    const [schedRes, resultsRes, strokesRes, distRes, rosterRes] = await Promise.all([
      this.loadScheduleUc.run(eventId),
      this.loadResultsUc.run(eventId),
      this.loadStrokesUc.run(),
      this.loadDistancesUc.run(),
      this.listSwimmersUc.run(undefined),
    ]);

    if (!schedRes.ok || !resultsRes.ok) {
      this.loading.set(false);
      this.error.set(true);
      return;
    }
    this.schedule.set(schedRes.data);
    this.results.set(resultsRes.data);
    this.strokes.set(strokesRes.ok ? strokesRes.data : []);
    this.distances.set(distRes.ok ? distRes.data : []);
    this.roster.set(rosterRes.ok ? rosterRes.data : []);
    this.loaded.set(true);
    this.loading.set(false);
  }

  openResults(sessionId: string): void {
    if (!this.canManage()) return;
    this.openRaceId.set(this.openRaceId() === sessionId ? null : sessionId);
  }
  isOpen(sessionId: string): boolean { return this.openRaceId() === sessionId; }

  timeInput(sessionId: string, swimmerId: string): string {
    return this.inputs()[sessionId]?.[swimmerId] ?? '';
  }
  setTime(sessionId: string, swimmerId: string, value: string): void {
    if (!this.canManage()) return;
    const all = { ...this.inputs() };
    all[sessionId] = { ...(all[sessionId] ?? {}), [swimmerId]: value };
    this.inputs.set(all);
  }

  async saveResults(sessionId: string): Promise<void> {
    if (!this.canManage() || this.saving()) return;
    const forSession = this.inputs()[sessionId] ?? {};
    const entries: { swimmerId: string; timeMs: number }[] = [];
    for (const [swimmerId, text] of Object.entries(forSession)) {
      const ms = parseTimeToMs(text);
      if (ms !== null) entries.push({ swimmerId, timeMs: ms });
    }
    if (entries.length === 0) return; // nothing valid to save
    this.saving.set(true);
    try {
      const res = await this.saveResultsUc.run({ eventId: this.eventId, raceSessionId: sessionId, entries });
      if (res.ok) {
        this.notify.success(this.i18n.t('championships.finished.saved'));
        this.results.set(res.data);
        this.openRaceId.set(null);
        const all = { ...this.inputs() };
        delete all[sessionId];
        this.inputs.set(all);
      } else {
        this.notify.error(this.i18n.t('championships.finished.saveFailed'));
      }
    } finally {
      this.saving.set(false);
    }
  }

  // ---- display helpers ----
  formatTime(ms: number): string { return formatMsToTime(ms); }
  swimmerName(s: SwimmerListItem): string {
    return this.language.lang() === 'ar' ? (s.nameAr ?? s.nameEn) : s.nameEn;
  }
  private swimmerNameById(id: string): string {
    const s = this.roster().find((x) => x.id === id);
    return s ? this.swimmerName(s) : id;
  }
  private raceName(distanceId: string, strokeId: string): string {
    return [this.lookupLabel(this.distances(), distanceId), this.lookupLabel(this.strokes(), strokeId)]
      .filter(Boolean).join(' ');
  }
  private dayLabel(day: RaceScheduleDay): string {
    return this.language.lang() === 'ar' ? (day.labelAr ?? day.labelEn) : day.labelEn;
  }
  private lookupLabel(items: LookupItem[], id: string): string {
    const item = items.find((i) => i.id === id);
    if (!item) return '';
    return this.language.lang() === 'ar' ? (item.nameAr ?? item.nameEn) : item.nameEn;
  }
}
