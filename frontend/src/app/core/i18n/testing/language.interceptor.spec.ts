import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors, HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { languageInterceptor } from '@core/i18n/language.interceptor';
import { LanguageStore } from '@core/i18n/language.store';
import type { Lang } from '@core/i18n/language.store';

describe('languageInterceptor', () => {
  let http: HttpClient;
  let mock: HttpTestingController;

  function setup(lang: Lang): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([languageInterceptor])),
        provideHttpClientTesting(),
        { provide: LanguageStore, useValue: { lang: () => lang } as unknown as LanguageStore },
      ],
    });
    http = TestBed.inject(HttpClient);
    mock = TestBed.inject(HttpTestingController);
  }
  afterEach(() => mock.verify());

  it('attaches Accept-Language: en when the site language is English', () => {
    setup('en');
    http.get('/x').subscribe();
    const req = mock.expectOne('/x');
    expect(req.request.headers.get('Accept-Language')).toBe('en');
    req.flush({});
  });

  it('attaches Accept-Language: ar when the site language is Arabic', () => {
    setup('ar');
    http.get('/x').subscribe();
    const req = mock.expectOne('/x');
    expect(req.request.headers.get('Accept-Language')).toBe('ar');
    req.flush({});
  });
});
