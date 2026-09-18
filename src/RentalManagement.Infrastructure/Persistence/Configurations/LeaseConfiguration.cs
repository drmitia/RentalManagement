using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentalManagement.Domain.Entities;

namespace RentalManagement.Infrastructure.Persistence.Configurations;

public class LeaseConfiguration : IEntityTypeConfiguration<Lease>
{
    public void Configure(EntityTypeBuilder<Lease> builder)
    {
        builder.HasOne(x => x.Unit)
            .WithMany(u => u.Leases)
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RentalApplication)
            .WithOne(a => a.Lease)
            .HasForeignKey<Lease>(x => x.RentalApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RentalApplicationId).IsUnique();
        builder.HasIndex(x => new { x.UnitId, x.StartDate, x.EndDate });
    }
}
