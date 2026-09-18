import { TestBed } from '@angular/core/testing';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n/translate.service';

describe('TranslateService', () => {
  beforeEach(() => { localStorage.clear(); TestBed.resetTestingModule(); });

  it('resolves dot-path keys for the active language', () => {
    const t = TestBed.inject(TranslateService);
    expect(t.t('shell.nav.settings')).toBe('Settings');
  });

  it('switches language reactively', () => {
    const lang = TestBed.inject(LanguageStore);
    const t = TestBed.inject(TranslateService);
    expect(t.t('shell.nav.settings')).toBe('Settings'); // en before switch
    lang.set('ar');
    expect(t.t('shell.nav.settings')).toBe('الإعدادات'); // ar after switch
  });

  it('returns the key itself when missing', () => {
    expect(TestBed.inject(TranslateService).t('nope.missing')).toBe('nope.missing');
  });
});
