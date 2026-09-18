using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RentalManagement.Domain.Entities;
using RentalManagement.Infrastructure.Identity;
using Unit = RentalManagement.Domain.Entities.Unit;

namespace RentalManagement.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<UnitType> UnitTypes => Set<UnitType>();
    public DbSet<RentalApplication> RentalApplications => Set<RentalApplication>();
    public DbSet<ApplicantInfo> ApplicantInfos => Set<ApplicantInfo>();
    public DbSet<ResidenceHistoryEntry> ResidenceHistoryEntries => Set<ResidenceHistoryEntry>();
    public DbSet<ApplicationApplicant> ApplicationApplicants => Set<ApplicationApplicant>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();
    public DbSet<Lease> Leases => Set<Lease>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
