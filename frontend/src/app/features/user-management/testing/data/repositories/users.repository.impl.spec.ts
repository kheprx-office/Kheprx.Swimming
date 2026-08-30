import { TestBed } from '@angular/core/testing';
import { UsersRepositoryImpl } from '@features/user-management/data/repositories/users.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';
import { UserDtoRs } from '@features/user-management/data/dto/user-management/user.dto';

const dto: UserDtoRs = { id: 'u1', fullName: 'A', email: 'a@b.c', role: 'admin', status: 'active', mustChangePassword: false };

describe('UsersRepositoryImpl', () => {
  const http = { get: jest.fn(), post: jest.fn(), patch: jest.fn() } as unknown as HttpClientService;
  let repo: UsersRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [UsersRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(UsersRepositoryImpl);
  });

  it('list GETs /api/users without params when no search is given', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [dto] });
    await expect(repo.list()).resolves.toEqual({ data: [dto] });
    expect(http.get).toHaveBeenCalledWith('/api/users', undefined);
  });

  it('list passes a trimmed search as a query param', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [dto] });
    await repo.list('  29801  ');
    expect(http.get).toHaveBeenCalledWith('/api/users', { params: { search: '29801' } });
  });

  it('create POSTs the request body and returns the envelope', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: dto });
    const rq = { fullName: 'A', email: 'a@b.c', role: 'admin', password: 'pw' };
    await expect(repo.create(rq)).resolves.toEqual({ data: dto });
    expect(http.post).toHaveBeenCalledWith('/api/users', { body: rq });
  });

  it('update PATCHes /api/users/:id', async () => {
    (http.patch as jest.Mock).mockResolvedValue({ data: dto });
    const rq = { fullName: 'A', email: 'a@b.c', status: 'active' };
    await expect(repo.update('u1', rq)).resolves.toEqual({ data: dto });
    expect(http.patch).toHaveBeenCalledWith('/api/users/u1', { body: rq });
  });

  it('setStatus PATCHes the status subresource', async () => {
    (http.patch as jest.Mock).mockResolvedValue({ data: dto });
    await expect(repo.setStatus('u1', { status: 'disabled' })).resolves.toEqual({ data: dto });
    expect(http.patch).toHaveBeenCalledWith('/api/users/u1/status', { body: { status: 'disabled' } });
  });
});
