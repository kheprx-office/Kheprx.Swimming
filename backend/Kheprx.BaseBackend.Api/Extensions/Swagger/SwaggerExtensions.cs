using Kheprx.BaseBackend.Api.Swagger;
using Microsoft.OpenApi.Models;

namespace Kheprx.BaseBackend.Api.Extensions.Swagger;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            // Namespace-qualify schema ids. This is a modular backend where the same DTO
            // name can live in different modules (e.g. two modules each defining a ProductDto);
            // Swashbuckle's default short-name schema ids would collide and throw at doc-gen.
            options.CustomSchemaIds(type => type.FullName ?? type.Name);

            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Kheprx Electric API",
                Version = "v1",
                Description = "Backend API for the Kheprx Electric platform. Every endpoint wraps its payload in the "
                    + "ApiResponse envelope: { successStatus, message, error, data }. Failure responses carry a "
                    + "machine-readable code in 'error' (e.g. EMAIL_IN_USE)."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste the accessToken returned by POST /api/auth/login."
            });

            options.OperationFilter<AuthorizeOperationFilter>();

            var documentedAssemblies = new[]
            {
                typeof(SwaggerExtensions).Assembly,
                typeof(Kheprx.BaseBackend.Identity.Application.DTOs.SessionDto).Assembly,
            };
            foreach (var assembly in documentedAssemblies)
            {
                var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
                if (File.Exists(xmlPath))
                    options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }
}
