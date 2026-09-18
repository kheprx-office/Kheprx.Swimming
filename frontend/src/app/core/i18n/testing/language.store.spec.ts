import { TestBed } from '@angular/core/testing';
import { LanguageStore } from '@core/i18n/language.store';

describe('LanguageStore', () => {
  beforeEach(() => { localStorage.clear(); TestBed.resetTestingModule(); });

  it('defaults to en / ltr and reflects onto <html>', () => {
    const store = TestBed.inject(LanguageStore);
    TestBed.tick(); // flush effect
    expect(store.lang()).toBe('en');
    expect(store.dir()).toBe('ltr');
    expect(document.documentElement.dir).toBe('ltr');
    expect(document.documentElement.lang).toBe('en');
  });

  it('toggle switches to ar / rtl and persists', () => {
    const store = TestBed.inject(LanguageStore);
    store.toggle();
    TestBed.tick();
    expect(store.lang()).toBe('ar');
    expect(store.dir()).toBe('rtl');
    expect(document.documentElement.dir).toBe('rtl');
    expect(localStorage.getItem('kheprx.lang')).toBe('ar');
  });

  it('reads the persisted language on construction', () => {
    localStorage.setItem('kheprx.lang', 'ar');
    expect(TestBed.inject(LanguageStore).lang()).toBe('ar');
  });
});
