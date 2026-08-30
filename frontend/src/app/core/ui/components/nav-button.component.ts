// NavButton: a reusable themed button (label + press output). No navigation logic.
import { Component, input, output } from '@angular/core';

@Component({
  selector: 'app-nav-button',
  standalone: true,
  template: `
    <button class="nav-button" [disabled]="disabled()" (click)="press.emit()">
      {{ label() }}
    </button>
  `,
  styles: [
    `
      .nav-button {
        background: var(--color-primary);
        color: var(--color-on-primary);
        border: none;
        padding: var(--space-md) var(--space-lg);
        border-radius: var(--radius-md);
        font-size: 16px;
        font-weight: 600;
        cursor: pointer;
      }
      .nav-button:disabled {
        opacity: 0.4;
        cursor: default;
      }
      .nav-button:not(:disabled):active {
        opacity: 0.7;
      }
    `,
  ],
})
export class NavButtonComponent {
  readonly label = input.required<string>();
  readonly disabled = input(false);
  readonly press = output<void>();
}
