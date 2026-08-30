import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors, HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { authInterceptor } from '@features/auth/data/auth.interceptor';
import { TokenStore } from '@features/auth/data/token-store';
import { RefreshTokenUseCase } from '@features/auth/domain/usecases/shared/refresh-token.use-case';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';

describe('authInterceptor', () => {
  let http: HttpClient;
  let mock: HttpTestingController;
  const tokenStore = { getAccess: jest.fn(), clear: jest.fn() } as unknown as TokenStore;
  const refresh = { run: jest.fn() } as unknown as RefreshTokenUseCase;
  const router = { navigate: jest.fn() } as unknown as Router;

  beforeEach(() => {
    jest.clearAllMocks();
    (tokenStore.getAccess as jest.Mock).mockResolvedValue(null);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: TokenStore, useValue: tokenStore },
        { provide: RefreshTokenUseCase, useValue: refresh },
        { provide: Router, useValue: router },
      ],
    });
    http = TestBed.inject(HttpClient);
    mock = TestBed.inject(HttpTestingController);
  });
  afterEach(() => mock.verify());

  it('attaches the bearer token', async () => {
    (tokenStore.getAccess as jest.Mock).mockResolvedValue('tok');
    http.get('/x').subscribe();
    await Promise.resolve();
    const req = mock.expectOne('/x');
    expect(req.request.headers.get('Authorization')).toBe('Bearer tok');
    req.flush({});
  });

  it('refreshes once on 401 and retries', async () => {
    (tokenStore.getAccess as jest.Mock).mockResolvedValue('old');
    (refresh.run as jest.Mock).mockResolvedValue(ok({ accessToken: 'new', refreshToken: 'r' }));
    const done = new Promise<void>((resolve) => http.get('/y').subscribe(() => resolve()));
    await Promise.resolve();
    mock.expectOne('/y').flush({ message: 'unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    await Promise.resolve(); await Promise.resolve();
    const retry = mock.expectOne('/y');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new');
    retry.flush({});
    await done;
    expect(refresh.run).toHaveBeenCalledTimes(1);
  });

  it('clears tokens and navigates to /login when refresh fails on 401', async () => {
    (tokenStore.getAccess as jest.Mock).mockResolvedValue('old');
    (refresh.run as jest.Mock).mockResolvedValue(fail(new AppError('expired', 'auth', 401)));
    let errorFired = false;
    const done = new Promise<void>((resolve) =>
      http.get('/z').subscribe({ error: () => { errorFired = true; resolve(); } }),
    );
    await Promise.resolve();
    mock.expectOne('/z').flush({ message: 'unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    await Promise.resolve(); await Promise.resolve();
    await done;
    expect(tokenStore.clear).toHaveBeenCalledTimes(1);
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
    expect(errorFired).toBe(true);
  });

  it('does not refresh when the refresh endpoint itself returns 401 (prevents the loop)', async () => {
    (tokenStore.getAccess as jest.Mock).mockResolvedValue('old');
    let errored = false;
    const done = new Promise<void>((resolve) =>
      http.post('/api/auth/refresh', {}).subscribe({ error: () => { errored = true; resolve(); } }),
    );
    await Promise.resolve();
    mock.expectOne('/api/auth/refresh').flush({ message: 'unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    await done;
    expect(refresh.run).not.toHaveBeenCalled();
    expect(errored).toBe(true);
  });

  it('does not refresh when login returns 401 (bad credentials)', async () => {
    (tokenStore.getAccess as jest.Mock).mockResolvedValue(null);
    const done = new Promise<void>((resolve) =>
      http.post('/api/auth/login', {}).subscribe({ error: () => resolve() }),
    );
    await Promise.resolve();
    mock.expectOne('/api/auth/login').flush({ message: 'bad' }, { status: 401, statusText: 'Unauthorized' });
    await done;
    expect(refresh.run).not.toHaveBeenCalled();
  });
});
