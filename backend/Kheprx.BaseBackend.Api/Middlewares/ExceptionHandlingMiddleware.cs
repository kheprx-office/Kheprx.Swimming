using Kheprx.BaseBackend.SharedKernel.Domain;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain rule violated");
            await WriteAsync(context, StatusCodes.Status400BadRequest, ex.Message, "DOMAIN_RULE_VIOLATION");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteAsync(context, StatusCodes.Status500InternalServerError,
                CommonMessages.Errors.ServerError(AppLanguage.Current), "SERVER_ERROR");
        }
    }

    private static Task WriteAsync(HttpContext context, int statusCode, string message, string error)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(ApiResponse<object>.Failure(message, error));
    }
}
