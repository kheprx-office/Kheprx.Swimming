using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kheprx.BaseBackend.Attendance.Infrastructure.Data;

// Design-time factory used by `dotnet ef`. Uses a local-dev connection string;
// at runtime the application uses ConnectionStrings:Postgres from configuration instead.
public sealed class AttendanceDbContextFactory : IDesignTimeDbContextFactory<AttendanceDbContext>
{
    public AttendanceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=basebackend;Username=postgres;Password=postgres")
            .Options;
        return new AttendanceDbContext(options);
    }
}
