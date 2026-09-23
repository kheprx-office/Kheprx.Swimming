import { Injectable, computed, inject, signal } from '@angular/core';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LoadChampionshipsUseCase } from '@features/championships/domain/usecases/load-championships.use-case';
import { CreateChampionshipUseCase } from '@features/championships/domain/usecases/create-championship.use-case';
import { Championship } from '@features/championships/domain/model/championship';
import { formatDateRange } from '@features/championships/presentation/pages/championships/format-date-range';

@Injectable()
export class ChampionshipsViewModel {
  private readonly loadChampionships = inject(LoadChampionshipsUseCase);
  private readonly createChampionship = inject(CreateChampionshipUseCase);
  private readonly language = inject(LanguageStore);
  private readonly session = inject(AuthSessionStore);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly loading = signal(true);
  readonly error = signal(false);
  private readonly all = signal<Championship[]>([]);
  readonly filterFrom = signal('');
  readonly filterTo = signal('');

  // ---- Create form state ----
  readonly showForm = signal(false);
  readonly saving = signal(false);
  readonly newName = signal('');
  readonly newStart = signal('');
  readonly newEnd = signal('');
  readonly newLocation = signal('');

  // Only Head Coach / Captain may create championships (button + save gate).
  readonly canManage = computed(() => {
    const r = this.session.role();
    return r === 'head_coach' || r === 'captain';
  });

  // Championships whose run overlaps the chosen period. Empty dates = show all.
  readonly filtered = computed<Championship[]>(() => {
    const from = this.filterFrom();
    const to = this.filterTo();
    return this.all().filter((e) => {
      if (from && e.endDate < from) return false;
      if (to && e.startDate > to) return false;
      return true;
    });
  });
  readonly count = computed(() => this.filtered().length);
  readonly hasFilter = computed(() => !!this.filterFrom() || !!this.filterTo());

  // Name + location required; end date not before start (ISO 'YYYY-MM-DD' compares lexicographically).
  readonly formValid = computed(() => {
    const start = this.newStart();
    const end = this.newEnd();
    return this.newName().trim().length > 0
      && this.newLocation().trim().length > 0
      && !!start && !!end && end >= start;
  });

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const res = await this.loadChampionships.run();
    if (res.ok) this.all.set(res.data);
    else { this.error.set(true); this.all.set([]); }
    this.loading.set(false);
  }

  setFrom(v: string): void { this.filterFrom.set(v); }
  setTo(v: string): void { this.filterTo.set(v); }
  clearFilter(): void { this.filterFrom.set(''); this.filterTo.set(''); }

  // ---- Create form actions ----
  openForm(): void { this.showForm.set(true); }
  cancelForm(): void { this.resetForm(); this.showForm.set(false); }
  setNewName(v: string): void { this.newName.set(v); }
  setNewStart(v: string): void { this.newStart.set(v); }
  setNewEnd(v: string): void { this.newEnd.set(v); }
  setNewLocation(v: string): void { this.newLocation.set(v); }

  private resetForm(): void {
    this.newName.set('');
    this.newStart.set('');
    this.newEnd.set('');
    this.newLocation.set('');
  }

  async save(): Promise<void> {
    if (!this.canManage() || !this.formValid() || this.saving()) return;
    this.saving.set(true);
    try {
      const res = await this.createChampionship.run({
        name: this.newName().trim(),
        startDate: this.newStart(),
        endDate: this.newEnd(),
        location: this.newLocation().trim(),
      });
      if (res.ok) {
        this.notify.success(this.i18n.t('championships.saved'));
        this.resetForm();
        this.showForm.set(false);
        await this.load();
      } else {
        this.notify.error(this.i18n.t('championships.saveFailed'));
      }
    } finally {
      this.saving.set(false);
    }
  }

  name(e: Championship): string {
    return this.language.lang() === 'ar' ? (e.nameAr ?? e.nameEn) : e.nameEn;
  }
  location(e: Championship): string {
    const ar = this.language.lang() === 'ar';
    return (ar ? e.locationAr ?? e.locationEn : e.locationEn) ?? '';
  }
  statusLabel(e: Championship): string {
    const ar = this.language.lang() === 'ar';
    return (ar ? e.statusNameAr ?? e.statusNameEn : e.statusNameEn) ?? '';
  }
  dateRange(e: Championship): string {
    return formatDateRange(e.startDate, e.endDate, this.language.lang() === 'ar' ? 'ar' : 'en');
  }
  statusBadgeClass(e: Championship): string {
    return e.statusCode === 'completed' ? 'bg-emerald-100 text-emerald-700' : 'bg-sky-100 text-sky-700';
  }
}
