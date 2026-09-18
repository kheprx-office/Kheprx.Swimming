using Kheprx.BaseBackend.Health.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Health.Infrastructure.Data;

public static class HealthSeeder
{
    // Demo `created_by` — a fixed, documented sentinel. medical_test.created_by has no FK
    // (cross-module decoupling), so this need not reference a real identity.app_user row.
    private static readonly Guid SeedCreatedBy = new("00000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(HealthDbContext db, CancellationToken ct = default)
    {
        if (await db.MedicalTests.AnyAsync(ct)) return;

        await db.MedicalTests.AddRangeAsync(new[]
        {
            new MedicalTest("Hemoglobin", "الهيموغلوبين", "g/dL", 11m, 17.5m, SeedCreatedBy),
            new MedicalTest("Glucose (FBS)", "الجلوكوز الصائم", "mg/dL", 70m, 110m, SeedCreatedBy),
            new MedicalTest("Uric Acid", "حمض اليوريك", "mg/dL", 1m, 7m, SeedCreatedBy),
        }, ct);
        await db.SaveChangesAsync(ct);
    }
}
