using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class ManagerConfiguration : IEntityTypeConfiguration<Manager>
{
    public void Configure(EntityTypeBuilder<Manager> builder)
    {
        builder.ToTable("managers");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.MonthlySalary).HasPrecision(14, 2);
        builder.HasIndex(m => m.UserId).IsUnique();
        builder.HasOne<User>().WithOne().HasForeignKey<Manager>(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
