import { Component, inject, input, model } from '@angular/core';
import { LookupItem } from '@features/reference/domain/model/reference';
import { LanguageStore } from '@core/i18n';

@Component({
  selector: 'app-chip-group',
  standalone: true,
  template: `
    <div class="flex flex-col gap-2">
      @if (label()) { <span class="text-sm font-bold text-ink">{{ label() }}</span> }
      <div class="flex flex-wrap gap-2">
        @for (opt of options(); track opt.id) {
          <button
            type="button"
            class="rounded-full border px-4 py-1.5 text-sm font-semibold transition-colors"
            [class]="selected().includes(opt.id) ? 'border-primary bg-primary text-white' : 'border-input bg-card text-ink'"
            (click)="toggle(opt.id)">
            {{ labelFor(opt) }}
          </button>
        }
      </div>
    </div>
  `,
})
export class ChipGroupComponent {
  readonly label = input('');
  readonly options = input<LookupItem[]>([]);
  readonly selected = model<string[]>([]);
  private readonly lang = inject(LanguageStore).lang;

  labelFor(opt: LookupItem): string {
    return this.lang() === 'ar' ? (opt.nameAr ?? opt.nameEn) : opt.nameEn;
  }

  toggle(id: string): void {
    const cur = this.selected();
    this.selected.set(cur.includes(id) ? cur.filter((x) => x !== id) : [...cur, id]);
  }
}
