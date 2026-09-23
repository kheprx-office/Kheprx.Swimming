import { Injectable, computed, inject, signal } from '@angular/core';
import { LoadAttendanceSessionUseCase } from '@features/attendance/domain/usecases/load-attendance-session.use-case';
import { SaveAttendanceSessionUseCase } from '@features/attendance/domain/usecases/save-attendance-session.use-case';
import { LoadAttendanceStatusesUseCase } from '@features/reference/domain/usecases/load-attendance-statuses.use-case';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';
import { LookupItem } from '@features/reference/domain/model/reference';
import { AttendanceSession, SessionRow } from '@features/attendance/domain/model/attendance-session';

export interface EntryRow extends Omit<SessionRow, 'statusId'> {
  statusId: string; // never null — defaulted to Present on load
}

function todayIso(): string {
  const d = new Date();
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

@Injectable()
export class AttendanceEntryViewModel {
  private readonly loadSession = inject(LoadAttendanceSessionUseCase);
  private readonly saveSession = inject(SaveAttendanceSessionUseCase);
  private readonly loadStatuses = inject(LoadAttendanceStatusesUseCase);
  private readonly session = inject(AuthSessionStore);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);
  private readonly language = inject(LanguageStore);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal(false);
  readonly selectedDate = signal<string>(todayIso());
  readonly statuses = signal<LookupItem[]>([]);
  readonly rows = signal<EntryRow[]>([]);

  // swimmerId → the server's saved state, for dirty comparison
  private original = new Map<string, { statusId: string | null; coachNote: string | null }>();

  readonly total = computed(() => this.rows().length);
  readonly presentCount = computed(() =>
    this.rows().filter((r) => { const c = this.statusCode(r.statusId); return c === 'present' || c === 'late'; }).length);

  readonly canSave = computed(() => {
    const role = this.session.role();
    return role === 'head_coach' || role === 'captain';
  });

  readonly dirty = computed(() => this.rows().some((r) => this.isRowDirty(r)));

  private presentId(): string | null {
    return this.statuses().find((s) => s.code === 'present')?.id ?? null;
  }

  statusCode(statusId: string | null): string {
    return this.statuses().find((s) => s.id === statusId)?.code ?? '';
  }

  rateBand(pct: number | null): 'red' | 'amber' | 'green' | 'muted' {
    if (pct === null || pct === undefined) return 'muted';
    if (pct < 70) return 'red';
    if (pct < 85) return 'amber';
    return 'green';
  }

  private isRowDirty(r: EntryRow): boolean {
    if (!r.hasRecord) return true; // fresh default — will be created on save
    const o = this.original.get(r.swimmerId);
    return r.statusId !== o?.statusId || (r.coachNote ?? '') !== (o?.coachNote ?? '');
  }

  setDate(date: string): void {
    this.selectedDate.set(date);
    void this.load(date);
  }

  setStatus(swimmerId: string, statusId: string): void {
    this.rows.update((rows) => rows.map((r) => (r.swimmerId === swimmerId ? { ...r, statusId } : r)));
  }

  setNote(swimmerId: string, coachNote: string): void {
    this.rows.update((rows) => rows.map((r) => (r.swimmerId === swimmerId ? { ...r, coachNote } : r)));
  }

  markAllPresent(): void {
    const pid = this.presentId();
    if (!pid) return;
    this.rows.update((rows) => rows.map((r) => ({ ...r, statusId: pid })));
  }

  async load(date?: string): Promise<void> {
    const d = date ?? this.selectedDate();
    this.selectedDate.set(d);
    this.loading.set(true);
    this.error.set(false);

    if (this.statuses().length === 0) {
      const s = await this.loadStatuses.run();
      if (s.ok) this.statuses.set(s.data);
    }

    const res = await this.loadSession.run(d);
    if (res.ok) {
      this.applySession(res.data);
    } else {
      this.error.set(true);
      this.rows.set([]);
    }
    this.loading.set(false);
  }

  async save(): Promise<void> {
    if (!this.canSave() || !this.dirty() || this.saving()) return;
    this.saving.set(true);
    try {
      const payload = {
        date: this.selectedDate(),
        entries: this.rows().map((r) => ({ swimmerId: r.swimmerId, statusId: r.statusId, coachNote: r.coachNote })),
      };
      const res = await this.saveSession.run(payload);
      if (res.ok) {
        this.applySession(res.data);
        this.notify.success(this.i18n.t('attendanceEntry.saved'));
      } else {
        this.notify.error(this.i18n.t('attendanceEntry.saveFailed'));
      }
    } finally {
      this.saving.set(false);
    }
  }

  private applySession(session: AttendanceSession): void {
    const pid = this.presentId();
    this.original = new Map(session.rows.map((r) => [r.swimmerId, { statusId: r.statusId, coachNote: r.coachNote }]));
    this.rows.set(session.rows.map((r) => ({ ...r, statusId: r.statusId ?? pid ?? '' })));
  }
}
