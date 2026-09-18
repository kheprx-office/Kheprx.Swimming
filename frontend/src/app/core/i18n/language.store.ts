import { Injectable, computed, effect, signal } from '@angular/core';

export type Lang = 'en' | 'ar';
const KEY = 'kheprx.lang';

@Injectable({ providedIn: 'root' })
export class LanguageStore {
  private readonly _lang = signal<Lang>(this.read());
  readonly lang = this._lang.asReadonly();
  readonly dir = computed<'ltr' | 'rtl'>(() => (this._lang() === 'ar' ? 'rtl' : 'ltr'));

  constructor() {
    effect(() => {
      const lang = this._lang();
      const el = document.documentElement;
      el.lang = lang;
      el.dir = lang === 'ar' ? 'rtl' : 'ltr';
      localStorage.setItem(KEY, lang);
    });
  }

  set(lang: Lang): void { this._lang.set(lang); }
  toggle(): void { this._lang.set(this._lang() === 'en' ? 'ar' : 'en'); }

  private read(): Lang {
    const v = localStorage.getItem(KEY);
    return v === 'ar' || v === 'en' ? v : 'en';
  }
}
