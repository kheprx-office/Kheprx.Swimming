using Kheprx.BaseBackend.Identity.Domain.Entities;

namespace Kheprx.BaseBackend.Identity.Application.Abstractions;

public interface IJwtTokenService
{
    string CreateAccessToken(AppUser user, string roleCode);
    (string token, string hash) CreateRefreshToken();
    string HashRefreshToken(string token);
}
