import { Component, input, model } from '@angular/core';

@Component({
  selector: 'app-date-field',
  standalone: true,
  template: `
    <label class="flex flex-col gap-1">
      @if (label()) { <span class="text-sm font-bold text-ink">{{ label() }}</span> }
      <input
        type="date"
        class="h-10 rounded-md border border-input bg-card px-3 text-sm text-ink focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
        [value]="value()"
        (input)="value.set($any($event.target).value)" />
    </label>
  `,
})
export class DateFieldComponent {
  readonly label = input('');
  readonly value = model('');
}
