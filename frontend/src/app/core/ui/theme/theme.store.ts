import { Injectable, effect, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark';
const KEY = 'kheprx.theme';

@Injectable({ providedIn: 'root' })
export class ThemeStore {
  private readonly _mode = signal<ThemeMode>(this.read());
  readonly mode = this._mode.asReadonly();

  constructor() {
    effect(() => {
      const mode = this._mode();
      document.documentElement.classList.toggle('dark', mode === 'dark');
      localStorage.setItem(KEY, mode);
    });
  }

  set(mode: ThemeMode): void { this._mode.set(mode); }
  toggle(): void { this._mode.set(this._mode() === 'light' ? 'dark' : 'light'); }

  private read(): ThemeMode { return localStorage.getItem(KEY) === 'dark' ? 'dark' : 'light'; }
}
