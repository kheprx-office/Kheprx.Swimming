import { TestBed } from '@angular/core/testing';
import { AuthRepositoryImpl } from '@features/auth/data/repositories/auth.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';
import { SessionDtoRs } from '@features/auth/data/dto/shared/session.dto';
import { CurrentUserDtoRs } from '@features/auth/data/dto/shared/current-user.dto';

const session: SessionDtoRs = { accessToken: 'a', refreshToken: 'r', role: 'admin', userId: 'USR-1', mustChangePassword: false };
const user: CurrentUserDtoRs = { userId: 'USR-1', email: 'a@b.c', fullName: 'A', role: 'admin' };

describe('AuthRepositoryImpl', () => {
  const http = { get: jest.fn(), post: jest.fn() } as unknown as HttpClientService;
  let repo: AuthRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [AuthRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(AuthRepositoryImpl);
  });

  it('login POSTs credentials and returns the session envelope', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: session });
    const rq = { email: 'a@b.c', password: 'pw' };
    await expect(repo.login(rq)).resolves.toEqual({ data: session });
    expect(http.post).toHaveBeenCalledWith('/api/auth/login', { body: rq });
  });

  it('refresh POSTs the refresh token and returns the session envelope', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: session });
    const rq = { refreshToken: 'r' };
    await expect(repo.refresh(rq)).resolves.toEqual({ data: session });
    expect(http.post).toHaveBeenCalledWith('/api/auth/refresh', { body: rq });
  });

  it('logout POSTs to the revoke endpoint with no body and resolves void', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: null });
    await expect(repo.logout()).resolves.toBeUndefined();
    expect(http.post).toHaveBeenCalledWith('/api/auth/logout', {});
  });

  it('me GETs the current-user envelope', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: user });
    await expect(repo.me()).resolves.toEqual({ data: user });
    expect(http.get).toHaveBeenCalledWith('/api/auth/me');
  });

  it('changePassword POSTs the password pair and returns the session envelope', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: session });
    const rq = { currentPassword: 'old', newPassword: 'new12345' };
    await expect(repo.changePassword(rq)).resolves.toEqual({ data: session });
    expect(http.post).toHaveBeenCalledWith('/api/auth/change-password', { body: rq });
  });
});
