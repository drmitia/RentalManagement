using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentalManagement.Domain.Entities;
using RentalManagement.Infrastructure.Identity;

namespace RentalManagement.Infrastructure.Persistence.Configurations;

public class ApplicationApplicantConfiguration : IEntityTypeConfiguration<ApplicationApplicant>
{
    public void Configure(EntityTypeBuilder<ApplicationApplicant> builder)
    {
        builder.HasKey(x => new { x.RentalApplicationId, x.UserId });

        builder.HasOne(x => x.RentalApplication)
            .WithMany(a => a.Applicants)
            .HasForeignKey(x => x.RentalApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
