using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.Options;
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Extensions;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AccountCreationOptions>(configuration.GetSection(AccountCreationOptions.SectionName));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ICoachProfileRepository, CoachProfileRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISwimmerProfileRepository, SwimmerProfileRepository>();
        services.AddScoped<ISwimmerService, SwimmerService>();
        services.AddScoped<ICoachService, CoachService>();
        services.AddScoped<IClubRepository, ClubRepository>();
        services.AddScoped<IBloodTypeRepository, BloodTypeRepository>();
        services.AddScoped<IFitnessAssessmentRepository, FitnessAssessmentRepository>();
        services.AddScoped<IObservationCategoryRepository, ObservationCategoryRepository>();
        services.AddScoped<IFeedbackCategoryRepository, FeedbackCategoryRepository>();
        services.AddScoped<IStrokeRepository, StrokeRepository>();
        services.AddScoped<IGenderRepository, GenderRepository>();
        services.AddScoped<IReferenceService, ReferenceService>();
        return services;
    }
}
