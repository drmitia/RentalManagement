using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentalManagement.Domain.Entities;

namespace RentalManagement.Infrastructure.Persistence.Configurations;

public class ResidenceHistoryEntryConfiguration : IEntityTypeConfiguration<ResidenceHistoryEntry>
{
    public void Configure(EntityTypeBuilder<ResidenceHistoryEntry> builder)
    {
        builder.Property(x => x.Address).IsRequired().HasMaxLength(300);
        builder.Property(x => x.LandlordName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.LandlordPhone).IsRequired().HasMaxLength(30);

        builder.HasOne(x => x.RentalApplication)
            .WithMany(a => a.ResidenceHistory)
            .HasForeignKey(x => x.RentalApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
