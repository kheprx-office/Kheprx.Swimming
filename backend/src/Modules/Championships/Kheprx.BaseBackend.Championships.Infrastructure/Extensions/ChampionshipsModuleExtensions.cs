using Kheprx.BaseBackend.Championships.Application.Services;
using Kheprx.BaseBackend.Championships.Application.Services.Interfaces;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Kheprx.BaseBackend.Championships.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Extensions;

public static class ChampionshipsModuleExtensions
{
    public static IServiceCollection AddChampionshipsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ChampionshipsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<ICompetitionEventRepository, CompetitionEventRepository>();
        services.AddScoped<IChampionshipEnrollmentRepository, ChampionshipEnrollmentRepository>();
        services.AddScoped<ICompetitionScheduleRepository, CompetitionScheduleRepository>();
        services.AddScoped<IRaceResultRepository, RaceResultRepository>();
        services.AddScoped<IChampionshipService, ChampionshipService>();
        return services;
    }
}
