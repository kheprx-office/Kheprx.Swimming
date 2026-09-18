import { Injectable, computed, inject, signal } from '@angular/core';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { LoadObservationCategoriesUseCase } from '@features/reference/domain/usecases/load-observation-categories.use-case';
import { CreateObservationUseCase } from '@features/observations/domain/usecases/create-observation.use-case';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import { LookupItem } from '@features/reference/domain/model/reference';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

@Injectable()
export class SwimmerDataViewModel {
  private readonly listSwimmers = inject(ListSwimmersUseCase);
  private readonly loadCategories = inject(LoadObservationCategoriesUseCase);
  private readonly createObservation = inject(CreateObservationUseCase);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly swimmers = signal<SwimmerListItem[]>([]);
  readonly categories = signal<LookupItem[]>([]);
  readonly loading = signal(false);
  readonly error = signal(false);

  readonly swimmerId = signal('');
  readonly categoryId = signal('');
  readonly fieldLabel = signal('');
  readonly value = signal('');
  readonly submitting = signal(false);

  readonly swimmerOptions = computed<LookupItem[]>(() =>
    this.swimmers().map((s) => ({ id: s.id, nameEn: s.nameEn, nameAr: s.nameAr })));

  readonly categoryOptions = computed<LookupItem[]>(() => this.categories());

  readonly canSubmit = computed(() =>
    this.swimmerId().length > 0
    && this.categoryId().length > 0
    && this.fieldLabel().trim().length > 0
    && this.value().trim().length > 0);

  constructor() { void this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const [s, c] = await Promise.all([this.listSwimmers.run(undefined), this.loadCategories.run()]);
    this.loading.set(false);
    if (s.ok) this.swimmers.set(s.data); else this.error.set(true);
    if (c.ok) this.categories.set(c.data); else this.error.set(true);
  }

  async submit(): Promise<void> {
    if (!this.canSubmit() || this.submitting()) return;
    this.submitting.set(true);
    const r = await this.createObservation.run({
      swimmerId: this.swimmerId(),
      categoryId: this.categoryId(),
      fieldLabel: this.fieldLabel().trim(),
      value: this.value().trim(),
    });
    this.submitting.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerData.toasts.added'));
      this.fieldLabel.set('');
      this.value.set('');
    } else {
      this.notify.error(this.i18n.t('swimmerData.toasts.addFailed'));
    }
  }
}
