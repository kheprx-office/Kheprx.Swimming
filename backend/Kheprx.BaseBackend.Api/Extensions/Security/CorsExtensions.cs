namespace Kheprx.BaseBackend.Api.Extensions.Security;

public static class CorsExtensions
{
    public const string CorsPolicyName = "Frontend";

    /// <summary>
    /// Configures the named CORS policy from the Cors:Origins configuration section.
    /// Falls back to allowing any origin when unconfigured (dev only).
    /// </summary>
    public static IServiceCollection AddCorsConfiguration(
        this IServiceCollection services, IConfiguration configuration)
    {
        var corsOrigins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
        {
            if (corsOrigins.Length > 0)
                policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod(); // no AllowCredentials — Bearer tokens
            else
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod(); // dev fallback when unconfigured
        }));
        return services;
    }
}
