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
        // 2. Request Localization (Accept-Language -> en/ar)
        // ========================================
        // Sets CurrentUICulture per request from the Accept-Language header (the frontend sends
        // "en" or "ar"). AppLanguage.Current reads it, so every localized message resolves here.
        // Anything unsupported or missing falls back to the default culture (en).
        var supportedCultures = new[] { "en", "ar" };
        app.UseRequestLocalization(new RequestLocalizationOptions()
            .SetDefaultCulture(supportedCultures[0])
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures));

        // ========================================
        // 3. Swagger (Development only)
        // ========================================
        if (environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // ========================================
        // 4. Request Logging
        // ========================================
        app.UseSerilogRequestLogging();

        // ========================================
        // 5. CORS
        // ========================================
        app.UseCors(CorsExtensions.CorsPolicyName);

        // ========================================
        // 6. Authentication
        // ========================================
        app.UseAuthentication();

        // ========================================
        // 7. Authorization
        // ========================================
        app.UseAuthorization();

        return app;
    }
}
