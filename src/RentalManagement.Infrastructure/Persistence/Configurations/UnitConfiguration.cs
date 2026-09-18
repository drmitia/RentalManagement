using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unit = RentalManagement.Domain.Entities.Unit;

namespace RentalManagement.Infrastructure.Persistence.Configurations;

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.Property(x => x.UnitNumber).IsRequired().HasMaxLength(50);
        builder.Property(x => x.MonthlyRent).HasPrecision(18, 2);

        builder.HasOne(x => x.Property)
            .WithMany(p => p.Units)
            .HasForeignKey(x => x.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.UnitType)
            .WithMany(t => t.Units)
            .HasForeignKey(x => x.UnitTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PropertyId, x.UnitNumber }).IsUnique();
    }
}
