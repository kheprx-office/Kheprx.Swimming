using Kheprx.BaseBackend.Api.Extensions.Data;
using Kheprx.BaseBackend.Api.Extensions.Mvc;
using Kheprx.BaseBackend.Api.Extensions.Pipeline;
using Kheprx.BaseBackend.Api.Extensions.Security;
using Kheprx.BaseBackend.Api.Extensions.Swagger;
using Kheprx.BaseBackend.Api.Extensions.Validation;
using Kheprx.BaseBackend.Identity.Infrastructure.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration));

// ========================================
// Configure Services
// ========================================
// Module registrations
builder.Services.AddIdentityModule(builder.Configuration);

// Cross-cutting concerns
builder.Services.AddMvcConfiguration();
builder.Services.AddSwaggerConfiguration();
builder.Services.AddFluentValidationConfiguration();
builder.Services.AddCorsConfiguration(builder.Configuration);
builder.Services.AddJwtAuth(builder.Configuration);

var app = builder.Build();

await app.ApplyIdentityMigrationsAsync();

// ========================================
// Configure Middleware Pipeline
// ========================================
app.UseBaseBackendMiddleware(app.Environment);
app.MapControllers();

// Anonymous liveness probe — used by the deploy health check and the Ocelot gateway.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
