// TextField: a labeled input with a two-way `value` model signal.
import { Component, input, model } from '@angular/core';

@Component({
  selector: 'app-text-field',
  standalone: true,
  template: `
    <label class="text-field">
      @if (label()) { <span class="label">{{ label() }}</span> }
      <input
        class="input"
        [value]="value()"
        [placeholder]="placeholder()"
        (input)="onInput($event)"
      />
    </label>
  `,
  styles: [
    `
      .text-field { display: flex; flex-direction: column; gap: var(--space-xs); }
      .label { color: var(--color-muted); font-size: 14px; }
      .input {
        padding: var(--space-sm) var(--space-md);
        border: 1px solid var(--color-border);
        border-radius: var(--radius-sm);
        font-size: 16px;
      }
    `,
  ],
})
export class TextFieldComponent {
  readonly label = input('');
  readonly placeholder = input('');
  readonly value = model('');

  onInput(event: Event): void {
    this.value.set((event.target as HTMLInputElement).value);
  }
}
