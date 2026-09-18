using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentalManagement.Domain;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Enums;
using RentalManagement.Infrastructure.Identity;
using RentalManagement.Infrastructure.Persistence;

namespace RentalManagement.Infrastructure.Seeding;

public static class DatabaseSeeder
{
    private const string SeedPassword = "Passw0rd!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await SeedRolesAsync(roleManager);
        var managers = await SeedUsersAsync(userManager, Roles.PropertyManager, "Manager", 2);
        var applicants = await SeedUsersAsync(userManager, Roles.Applicant, "Applicant", 5);

        var unitTypes = await SeedUnitTypesAsync(db);
        var units = await SeedPropertiesAndUnitsAsync(db, unitTypes);
        await SeedApplicationsAsync(db, units, applicants, managers);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { Roles.Applicant, Roles.PropertyManager })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task<List<ApplicationUser>> SeedUsersAsync(
        UserManager<ApplicationUser> userManager, string role, string namePrefix, int count)
    {
        var users = new List<ApplicationUser>();
        var faker = new Faker();

        for (var i = 1; i <= count; i++)
        {
            var email = $"{namePrefix.ToLowerInvariant()}{i}@rentalmanagement.test";
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = faker.Name.FullName()
                };
                var result = await userManager.CreateAsync(user, SeedPassword);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to seed user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }

            users.Add(user);
        }

        return users;
    }

    private static async Task<List<UnitType>> SeedUnitTypesAsync(AppDbContext db)
    {
        var names = new (string Name, bool IsActive)[]
        {
            ("Studio", true),
            ("1 Bedroom", true),
            ("2 Bedroom", true),
            ("3 Bedroom", true),
            ("Penthouse", false)
        };

        foreach (var (name, isActive) in names)
        {
            if (!await db.UnitTypes.AnyAsync(t => t.Name == name))
            {
                db.UnitTypes.Add(new UnitType { Name = name, IsActive = isActive });
            }
        }

        await db.SaveChangesAsync();
        return await db.UnitTypes.ToListAsync();
    }

    private static async Task<List<Unit>> SeedPropertiesAndUnitsAsync(AppDbContext db, List<UnitType> unitTypes)
    {
        if (await db.Properties.AnyAsync())
        {
            return await db.Units.Include(u => u.Property).Include(u => u.UnitType).ToListAsync();
        }

        var activeTypes = unitTypes.Where(t => t.IsActive).ToList();
        var faker = new Faker();
        var propertyFaker = new Faker<Property>()
            .RuleFor(p => p.Name, f => $"{f.Address.StreetName()} {f.PickRandom("Apartments", "Residences", "Towers", "Court")}")
            .RuleFor(p => p.Address, f => f.Address.FullAddress());

        var properties = propertyFaker.Generate(3);
        db.Properties.AddRange(properties);
        await db.SaveChangesAsync();

        foreach (var property in properties)
        {
            var unitCount = faker.Random.Int(4, 6);
            var unitNumbers = Enumerable.Range(1, unitCount)
                .Select(i => $"{(i - 1) / 4 + 1}{(char)('A' + (i - 1) % 4)}")
                .ToList();

            var unitFaker = new Faker<Unit>()
                .RuleFor(u => u.PropertyId, property.Id)
                .RuleFor(u => u.UnitNumber, f => unitNumbers[f.IndexFaker])
                .RuleFor(u => u.Bedrooms, f => f.Random.Int(0, 4))
                .RuleFor(u => u.MonthlyRent, f => f.Random.Decimal(900, 3500))
                .RuleFor(u => u.UnitTypeId, f => f.PickRandom(activeTypes).Id);

            db.Units.AddRange(unitFaker.Generate(unitCount));
        }

        await db.SaveChangesAsync();
        return await db.Units.Include(u => u.Property).Include(u => u.UnitType).ToListAsync();
    }

    private static async Task SeedApplicationsAsync(
        AppDbContext db, List<Unit> units, List<ApplicationUser> applicants, List<ApplicationUser> managers)
    {
        if (await db.RentalApplications.AnyAsync())
        {
            return;
        }

        var faker = new Faker();
        var manager = managers[0];
        var statuses = new[]
        {
            ApplicationStatus.Draft,
            ApplicationStatus.Submitted,
            ApplicationStatus.Returned,
            ApplicationStatus.Approved,
            ApplicationStatus.Denied,
            ApplicationStatus.Withdrawn
        };

        for (var i = 0; i < statuses.Length; i++)
        {
            var status = statuses[i];
            var unit = units[i % units.Count];
            var applicant = applicants[i % applicants.Count];

            var application = new RentalApplication
            {
                UnitId = unit.Id,
                Status = status,
                IsApplicantInfoComplete = status != ApplicationStatus.Draft || faker.Random.Bool(),
                CreatedAt = DateTime.UtcNow.AddDays(-faker.Random.Int(5, 60))
            };
            application.IsResidenceHistoryComplete = application.IsApplicantInfoComplete && status != ApplicationStatus.Draft;

            application.ApplicantInfo = new ApplicantInfo
            {
                FullName = applicant.FullName,
                Phone = faker.Phone.PhoneNumber(),
                Email = applicant.Email!,
                CurrentAddress = faker.Address.FullAddress()
            };

            application.ResidenceHistory.Add(new ResidenceHistoryEntry
            {
                Address = faker.Address.FullAddress(),
                LandlordName = faker.Name.FullName(),
                LandlordPhone = faker.Phone.PhoneNumber(),
                MoveInDate = DateOnly.FromDateTime(faker.Date.Past(5, DateTime.UtcNow.AddYears(-1))),
                MoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1))
            });

            application.Applicants.Add(new ApplicationApplicant { UserId = applicant.Id });

            db.RentalApplications.Add(application);
            await db.SaveChangesAsync();

            AddHistory(db, application, ApplicationStatus.Draft, ApplicationStatus.Draft, applicant.Id, null);

            if (status is ApplicationStatus.Submitted or ApplicationStatus.Returned or ApplicationStatus.Approved or ApplicationStatus.Denied)
            {
                application.SubmittedAt = DateTime.UtcNow.AddDays(-faker.Random.Int(1, 4));
                AddHistory(db, application, ApplicationStatus.Draft, ApplicationStatus.Submitted, applicant.Id, null);
            }

            switch (status)
            {
                case ApplicationStatus.Returned:
                    AddHistory(db, application, ApplicationStatus.Submitted, ApplicationStatus.Returned, manager.Id, "Please clarify move-out date on residence history.");
                    break;
                case ApplicationStatus.Approved:
                    AddHistory(db, application, ApplicationStatus.Submitted, ApplicationStatus.Approved, manager.Id, "Looks good, approved.");
                    var start = DateOnly.FromDateTime(DateTime.UtcNow.Date);
                    db.Leases.Add(new Lease
                    {
                        UnitId = unit.Id,
                        RentalApplicationId = application.Id,
                        StartDate = start,
                        EndDate = start.AddMonths(12)
                    });
                    break;
                case ApplicationStatus.Denied:
                    AddHistory(db, application, ApplicationStatus.Submitted, ApplicationStatus.Denied, manager.Id, "Insufficient rental history.");
                    break;
                case ApplicationStatus.Withdrawn:
                    AddHistory(db, application, ApplicationStatus.Draft, ApplicationStatus.Withdrawn, applicant.Id, null);
                    break;
            }

            await db.SaveChangesAsync();
        }
    }

    private static void AddHistory(
        AppDbContext db, RentalApplication application, ApplicationStatus from, ApplicationStatus to, string userId, string? comment)
    {
        db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            RentalApplicationId = application.Id,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = userId,
            Comment = comment,
            ChangedAt = DateTime.UtcNow
        });
    }
}
