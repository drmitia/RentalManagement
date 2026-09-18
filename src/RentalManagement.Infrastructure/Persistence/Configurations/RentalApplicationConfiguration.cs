using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentalManagement.Domain.Entities;

namespace RentalManagement.Infrastructure.Persistence.Configurations;

public class RentalApplicationConfiguration : IEntityTypeConfiguration<RentalApplication>
{
    public void Configure(EntityTypeBuilder<RentalApplication> builder)
    {
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.ResidenceHistoryVersion).IsConcurrencyToken();

        builder.HasOne(x => x.Unit)
            .WithMany(u => u.Applications)
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Status);
    }
}
