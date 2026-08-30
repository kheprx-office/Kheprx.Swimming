using System.Text.Json;
using Kheprx.BaseBackend.Api.Filters;
using Kheprx.BaseBackend.Api.Validation;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Extensions.Mvc;

public static class MvcExtensions
{
    public static IServiceCollection AddMvcConfiguration(this IServiceCollection services)
    {
        services.AddControllers(options => options.Filters.Add<ValidationFilter>())
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
            .ConfigureApiBehaviorOptions(options =>
                options.InvalidModelStateResponseFactory = context =>
                    new BadRequestObjectResult(ModelStateResponse.From(context.ModelState)));

        return services;
    }
}
