using Kheprx.BaseBackend.Attendance.Infrastructure.Data;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Api.Extensions.Data;

public static class MigrationExtensions
{
    public static async Task ApplyIdentityMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await IdentitySeeder.SeedAsync(db, hasher);
    }

    public static async Task ApplyHealthMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HealthDbContext>();
        await db.Database.MigrateAsync();
        await HealthSeeder.SeedAsync(db);
    }

    public static async Task ApplyAttendanceMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        await db.Database.MigrateAsync();
    }

    public static async Task ApplyChampionshipsMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChampionshipsDbContext>();
        await db.Database.MigrateAsync();
    }
}
