import { Injectable, computed, inject, signal } from '@angular/core';
import { LoadClubsUseCase, LoadGendersUseCase, LoadBloodTypesUseCase, LoadStrokesUseCase } from '@features/reference';
import { LookupItem } from '@features/reference/domain/model/reference';
import { CreateSwimmerUseCase } from '@features/swimmers/domain/usecases/create-swimmer.use-case';
import { CreatedSwimmer } from '@features/swimmers/domain/model/swimmer';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

@Injectable()
export class RegisterSwimmerViewModel {
  private readonly loadClubs = inject(LoadClubsUseCase);
  private readonly loadGenders = inject(LoadGendersUseCase);
  private readonly loadBloodTypes = inject(LoadBloodTypesUseCase);
  private readonly loadStrokes = inject(LoadStrokesUseCase);
  private readonly createSwimmer = inject(CreateSwimmerUseCase);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly clubs = signal<LookupItem[]>([]);
  readonly genders = signal<LookupItem[]>([]);
  readonly bloodTypes = signal<LookupItem[]>([]);
  readonly strokes = signal<LookupItem[]>([]);

  readonly nameEn = signal('');
  readonly nameAr = signal('');
  readonly username = signal('');
  readonly email = signal('');
  readonly phone = signal('');
  readonly genderId = signal('');
  readonly dob = signal('');
  readonly bloodTypeId = signal('');
  readonly trainingClubId = signal('');
  readonly championshipClubId = signal('');
  readonly strokeIds = signal<string[]>([]);

  readonly loading = signal(false);
  readonly created = signal<CreatedSwimmer | null>(null);

  readonly canSubmit = computed(() =>
    this.nameEn().trim().length > 0 &&
    this.username().trim().length > 0 &&
    this.trainingClubId().length > 0 &&
    this.genderId().length > 0 &&
    this.dob().length > 0 &&
    this.bloodTypeId().length > 0 &&
    this.strokeIds().length > 0);

  constructor() { void this.loadLookups(); }

  private async loadLookups(): Promise<void> {
    const [c, g, b, s] = await Promise.all([
      this.loadClubs.run(), this.loadGenders.run(), this.loadBloodTypes.run(), this.loadStrokes.run(),
    ]);
    if (c.ok) this.clubs.set(c.data);
    if (g.ok) this.genders.set(g.data);
    if (b.ok) this.bloodTypes.set(b.data);
    if (s.ok) this.strokes.set(s.data);
  }

  async submit(): Promise<void> {
    if (!this.canSubmit() || this.loading()) return;
    this.loading.set(true);
    const r = await this.createSwimmer.run({
      nameEn: this.nameEn().trim(),
      username: this.username().trim(),
      trainingClubId: this.trainingClubId(),
      genderId: this.genderId(),
      dob: this.dob(),
      bloodTypeId: this.bloodTypeId(),
      strokeIds: this.strokeIds(),
      nameAr: this.nameAr().trim() || undefined,
      email: this.email().trim() || undefined,
      phone: this.phone().trim() || undefined,
      representChampionshipClubId: this.championshipClubId() || undefined,
    });
    this.loading.set(false);
    if (r.ok) {
      this.created.set(r.data);
      this.notify.success(this.i18n.t('accountCreation.success'));
    } else {
      const key = r.error.status === 409 ? 'accountCreation.errors.usernameTaken' : 'accountCreation.errors.failed';
      this.notify.error(this.i18n.t(key));
    }
  }

  reset(): void {
    this.created.set(null);
    for (const s of [this.nameEn, this.nameAr, this.username, this.email, this.phone,
      this.genderId, this.dob, this.bloodTypeId, this.trainingClubId, this.championshipClubId]) s.set('');
    this.strokeIds.set([]);
  }
}
