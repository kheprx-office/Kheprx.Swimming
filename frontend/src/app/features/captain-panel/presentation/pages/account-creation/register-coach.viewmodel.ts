import { Injectable, computed, inject, signal } from '@angular/core';
import { LoadGendersUseCase } from '@features/reference';
import { LookupItem } from '@features/reference/domain/model/reference';
import { CreateCoachUseCase } from '@features/coaches/domain/usecases/create-coach.use-case';
import { CreatedCoach } from '@features/coaches/domain/model/coach';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

@Injectable()
export class RegisterCoachViewModel {
  private readonly loadGenders = inject(LoadGendersUseCase);
  private readonly createCoach = inject(CreateCoachUseCase);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly genders = signal<LookupItem[]>([]);

  readonly role = signal<'captain' | 'head_coach'>('captain');
  readonly nameEn = signal('');
  readonly nameAr = signal('');
  readonly username = signal('');
  readonly email = signal('');
  readonly phone = signal('');
  readonly nationalId = signal('');
  readonly genderId = signal('');
  readonly dob = signal('');

  readonly loading = signal(false);
  readonly created = signal<CreatedCoach | null>(null);

  readonly canSubmit = computed(() =>
    this.nameEn().trim().length > 0 &&
    this.username().trim().length > 0 &&
    this.email().trim().length > 0 &&
    /^\d{14}$/.test(this.nationalId().trim()) &&
    this.genderId().length > 0 &&
    this.dob().length > 0 &&
    this.phone().trim().length > 0);

  constructor() { void this.loadGendersLookup(); }

  private async loadGendersLookup(): Promise<void> {
    const g = await this.loadGenders.run();
    if (g.ok) this.genders.set(g.data);
  }

  async submit(): Promise<void> {
    if (!this.canSubmit() || this.loading()) return;
    this.loading.set(true);
    const r = await this.createCoach.run({
      role: this.role(),
      nameEn: this.nameEn().trim(),
      username: this.username().trim(),
      email: this.email().trim(),
      nationalId: this.nationalId().trim(),
      genderId: this.genderId(),
      dob: this.dob(),
      phone: this.phone().trim(),
      nameAr: this.nameAr().trim() || undefined,
    });
    this.loading.set(false);
    if (r.ok) {
      this.created.set(r.data);
      this.notify.success(this.i18n.t('accountCreation.success'));
    } else {
      const key = r.error.status === 409 ? 'accountCreation.errors.taken' : 'accountCreation.errors.failed';
      this.notify.error(this.i18n.t(key));
    }
  }

  reset(): void {
    this.created.set(null);
    for (const s of [this.nameEn, this.nameAr, this.username, this.email, this.phone, this.nationalId, this.genderId, this.dob]) s.set('');
    this.role.set('captain');
  }
}
