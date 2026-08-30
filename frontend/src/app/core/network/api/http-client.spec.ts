import { TestBed, fakeAsync, tick, flushMicrotasks } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpClientService } from '@core/network/api/http-client';
import { env } from '@core/config/env';
import { AppError } from '@core/domain/errors/app-error';

describe('HttpClientService', () => {
  let svc: HttpClientService;
  let httpMock: HttpTestingController;
  const base = env.api.baseUrl;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    svc = TestBed.inject(HttpClientService);
    httpMock = TestBed.inject(HttpTestingController);
  });
  afterEach(() => httpMock.verify());

  it('POSTs a body and resolves the response', async () => {
    const p = svc.post<{ id: number }>('/things', { body: { name: 'x' } });
    const req = httpMock.expectOne(`${base}/things`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'x' });
    req.flush({ id: 1 });
    await expect(p).resolves.toEqual({ id: 1 });
  });

  it('serializes query params on GET', async () => {
    const p = svc.get('/things', { params: { page: 2, q: 'a' } });
    const req = httpMock.expectOne((r) => r.url === `${base}/things`);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('q')).toBe('a');
    req.flush([]);
    await p;
  });

  it('maps a 401 to AppError(auth)', async () => {
    const p = svc.get('/secure');
    httpMock
      .expectOne(`${base}/secure`)
      .flush({ message: 'nope' }, { status: 401, statusText: 'Unauthorized' });
    await expect(p).rejects.toMatchObject({ kind: 'auth', status: 401, message: 'nope' });
  });

  it('maps a 500 with an error body to AppError(http) using the body message + code', async () => {
    const p = svc.get('/boom');
    httpMock
      .expectOne(`${base}/boom`)
      .flush({ message: 'server exploded', code: 'E_BOOM' }, { status: 500, statusText: 'Error' });
    await expect(p).rejects.toMatchObject({
      kind: 'http',
      status: 500,
      message: 'server exploded',
      code: 'E_BOOM',
    });
  });

  it('retries transport failures (status 0) with backoff, then maps to network', fakeAsync(() => {
    let captured: AppError | undefined;
    svc.get('/posts').catch((e) => (captured = e as AppError));

    httpMock.expectOne(`${base}/posts`).error(new ProgressEvent('error'), { status: 0 });
    tick(500); // backoff before retry 1
    httpMock.expectOne(`${base}/posts`).error(new ProgressEvent('error'), { status: 0 });
    tick(1500); // backoff before retry 2
    httpMock.expectOne(`${base}/posts`).error(new ProgressEvent('error'), { status: 0 });
    flushMicrotasks();

    expect(captured).toBeInstanceOf(AppError);
    expect(captured!.kind).toBe('network');
  }));

  it('fetches binary content as a Blob', async () => {
    const p = svc.getBlob('/api/tasks/photos/abc/content');
    const req = httpMock.expectOne(`${base}/api/tasks/photos/abc/content`);
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['bytes'], { type: 'image/jpeg' }));
    await expect(p).resolves.toBeInstanceOf(Blob);
  });

  it('maps a 404 on a blob fetch to AppError(http)', async () => {
    const p = svc.getBlob('/api/tasks/photos/missing/content');
    httpMock
      .expectOne(`${base}/api/tasks/photos/missing/content`)
      .flush(null, { status: 404, statusText: 'Not Found' });
    await expect(p).rejects.toMatchObject({ kind: 'http', status: 404 });
  });
});
