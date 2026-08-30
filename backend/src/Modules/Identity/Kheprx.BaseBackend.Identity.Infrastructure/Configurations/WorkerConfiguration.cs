using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class WorkerConfiguration : IEntityTypeConfiguration<Worker>
{
    public void Configure(EntityTypeBuilder<Worker> builder)
    {
        builder.ToTable("workers");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.DailyWage).HasPrecision(14, 2);
        builder.HasIndex(w => w.UserId).IsUnique();
        builder.HasOne<User>().WithOne().HasForeignKey<Worker>(w => w.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
