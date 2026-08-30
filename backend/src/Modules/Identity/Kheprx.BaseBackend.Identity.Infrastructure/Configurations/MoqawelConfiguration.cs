using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class MoqawelConfiguration : IEntityTypeConfiguration<Moqawel>
{
    public void Configure(EntityTypeBuilder<Moqawel> builder)
    {
        builder.ToTable("moqaweleen");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.DailyWage).HasPrecision(14, 2);
        builder.HasIndex(m => m.UserId).IsUnique();
        builder.HasOne<User>().WithOne().HasForeignKey<Moqawel>(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
