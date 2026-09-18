import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors, HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { loadingInterceptor } from './loading.interceptor';
import { LoadingService } from './loading.service';

describe('loadingInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let loading: LoadingService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([loadingInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    loading = TestBed.inject(LoadingService);
  });

  afterEach(() => httpMock.verify());

  it('flags loading while a request is in flight and clears it on completion', () => {
    expect(loading.isLoading()).toBe(false);
    http.get('/api/anything').subscribe();
    expect(loading.isLoading()).toBe(true);
    httpMock.expectOne('/api/anything').flush({});
    expect(loading.isLoading()).toBe(false);
  });

  it('clears loading even when the request errors', () => {
    http.get('/api/boom').subscribe({ error: () => undefined });
    expect(loading.isLoading()).toBe(true);
    httpMock.expectOne('/api/boom').flush('nope', { status: 500, statusText: 'Server Error' });
    expect(loading.isLoading()).toBe(false);
  });

  it('does not flag loading for silent auth requests', () => {
    http.post('/api/auth/refresh', {}).subscribe();
    expect(loading.isLoading()).toBe(false);
    httpMock.expectOne('/api/auth/refresh').flush({});
    expect(loading.isLoading()).toBe(false);
  });
});
