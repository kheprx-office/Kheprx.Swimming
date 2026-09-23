using Kheprx.BaseBackend.Attendance.Application.Services;
using Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
using Kheprx.BaseBackend.Attendance.Domain.Repositories;
using Kheprx.BaseBackend.Attendance.Infrastructure.Data;
using Kheprx.BaseBackend.Attendance.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kheprx.BaseBackend.Attendance.Infrastructure.Extensions;

public static class AttendanceModuleExtensions
{
    public static IServiceCollection AddAttendanceModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AttendanceDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<IAttendanceRecordRepository, AttendanceRecordRepository>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        return services;
    }
}
