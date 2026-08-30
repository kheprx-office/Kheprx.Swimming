using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kheprx.BaseBackend.Api.Swagger;

/// <summary>
/// Derives 401/403 documentation and the Bearer security requirement from the real
/// [Authorize] attributes, so the doc can never drift from enforcement.
/// </summary>
public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            return;

        var authorizeAttributes = context.MethodInfo.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(context.MethodInfo.DeclaringType?.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                    ?? Enumerable.Empty<AuthorizeAttribute>())
            .ToArray();

        if (authorizeAttributes.Length == 0)
            return;

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            }] = Array.Empty<string>()
        });

        operation.Responses.TryAdd("401", new OpenApiResponse
        {
            Description = "Missing or invalid access token."
        });

        if (authorizeAttributes.Any(a => !string.IsNullOrWhiteSpace(a.Roles)))
            operation.Responses.TryAdd("403", new OpenApiResponse
            {
                Description = "Authenticated but lacking the required role."
            });
    }
}
