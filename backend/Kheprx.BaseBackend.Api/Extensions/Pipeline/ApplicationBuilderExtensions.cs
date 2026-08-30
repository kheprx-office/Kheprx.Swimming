using Kheprx.BaseBackend.Api.Extensions.Security;
using Kheprx.BaseBackend.Api.Middlewares;
using Serilog;

namespace Kheprx.BaseBackend.Api.Extensions.Pipeline;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseBaseBackendMiddleware(this WebApplication app, IWebHostEnvironment environment)
    {
        // ========================================
        // 1. Exception Handling
        // ========================================
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        // ========================================
        // 2. Swagger (Development only)
        // ========================================
        if (environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // ========================================
        // 3. Request Logging
        // ========================================
        app.UseSerilogRequestLogging();

        // ========================================
        // 4. CORS
        // ========================================
        app.UseCors(CorsExtensions.CorsPolicyName);

        // ========================================
        // 5. Authentication
        // ========================================
        app.UseAuthentication();

        // ========================================
        // 6. Authorization
        // ========================================
        app.UseAuthorization();

        return app;
    }
}
