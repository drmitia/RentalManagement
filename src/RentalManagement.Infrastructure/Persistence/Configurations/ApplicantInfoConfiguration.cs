using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentalManagement.Domain.Entities;

namespace RentalManagement.Infrastructure.Persistence.Configurations;

public class ApplicantInfoConfiguration : IEntityTypeConfiguration<ApplicantInfo>
{
    public void Configure(EntityTypeBuilder<ApplicantInfo> builder)
    {
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Property(x => x.FullName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Phone).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CurrentAddress).IsRequired().HasMaxLength(300);

        builder.HasOne(x => x.RentalApplication)
            .WithOne(a => a.ApplicantInfo)
            .HasForeignKey<ApplicantInfo>(x => x.RentalApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.RentalApplicationId).IsUnique();
    }
}
