using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected string AcceptLanguage => Request.Headers.AcceptLanguage.ToString();

    // The authenticated user's id, from the JWT `sub` claim (Guid.Empty if absent/unparsable).
    protected Guid CurrentUserId()
        => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : Guid.Empty;
}
