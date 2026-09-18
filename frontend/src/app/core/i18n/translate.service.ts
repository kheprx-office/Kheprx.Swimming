import { Injectable, computed, inject } from '@angular/core';
import { LanguageStore } from './language.store';
import { DICTIONARIES, Dict } from './dictionaries';

@Injectable({ providedIn: 'root' })
export class TranslateService {
  private readonly language = inject(LanguageStore);
  private readonly resolver = computed(() => {
    const dict = DICTIONARIES[this.language.lang()];
    return (key: string): string => lookup(dict, key) ?? key;
  });
  t(key: string): string { return this.resolver()(key); }
}

function lookup(dict: Dict, key: string): string | null {
  const val = key.split('.').reduce<unknown>(
    (acc, k) => (acc && typeof acc === 'object' ? (acc as Dict)[k] : undefined), dict);
  return typeof val === 'string' ? val : null;
}
