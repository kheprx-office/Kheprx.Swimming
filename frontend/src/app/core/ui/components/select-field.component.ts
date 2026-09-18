import { Component, inject, input, model } from '@angular/core';
import { LookupItem } from '@features/reference/domain/model/reference';
import { LanguageStore } from '@core/i18n';

@Component({
  selector: 'app-select-field',
  standalone: true,
  template: `
    <label class="flex flex-col gap-1">
      @if (label()) { <span class="text-sm font-bold text-ink">{{ label() }}</span> }
      <select
        class="h-10 rounded-md border border-input bg-card px-3 text-sm text-ink focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
        [value]="value()"
        (change)="value.set($any($event.target).value)">
        <option value="">{{ placeholder() }}</option>
        @for (opt of options(); track opt.id) {
          <option [value]="opt.id">{{ labelFor(opt) }}</option>
        }
      </select>
    </label>
  `,
})
export class SelectFieldComponent {
  readonly label = input('');
  readonly placeholder = input('');
  readonly options = input<LookupItem[]>([]);
  readonly value = model('');
  private readonly lang = inject(LanguageStore).lang;

  labelFor(opt: LookupItem): string {
    return this.lang() === 'ar' ? (opt.nameAr ?? opt.nameEn) : opt.nameEn;
  }
}
