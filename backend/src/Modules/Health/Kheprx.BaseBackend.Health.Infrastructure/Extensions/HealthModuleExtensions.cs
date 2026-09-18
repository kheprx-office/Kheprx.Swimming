using Kheprx.BaseBackend.Health.Application.Services;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Kheprx.BaseBackend.Health.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kheprx.BaseBackend.Health.Infrastructure.Extensions;

public static class HealthModuleExtensions
{
    public static IServiceCollection AddHealthModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<HealthDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<IMedicalTestRepository, MedicalTestRepository>();
        services.AddScoped<IMedicalTestService, MedicalTestService>();
        services.AddScoped<IHealthReadingRepository, HealthReadingRepository>();
        services.AddScoped<IObservationRepository, ObservationRepository>();
        services.AddScoped<IObservationService, ObservationService>();
        services.AddScoped<IHealthReadingService, HealthReadingService>();
        return services;
    }
}
