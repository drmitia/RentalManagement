using FluentAssertions;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Exceptions;
using RentalManagement.Infrastructure.Services;
using Xunit;

namespace RentalManagement.Tests;

public class UnitServiceTests : IClassFixture<SqliteAppDbContextFactory>
{
    private readonly SqliteAppDbContextFactory _factory;

    public UnitServiceTests(SqliteAppDbContextFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateAsync_rejects_inactive_unit_type()
    {
        using var db = _factory.CreateContext();
        var property = new Property { Name = "P1", Address = "A1" };
        var inactiveType = new UnitType { Name = "Penthouse", IsActive = false };
        db.Properties.Add(property);
        db.UnitTypes.Add(inactiveType);
        await db.SaveChangesAsync();

        var service = new UnitService(db);

        var act = () => service.CreateAsync(property.Id, "1A", 2, 1000m, inactiveType.Id);

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task UpdateAsync_allows_keeping_a_unit_that_already_has_an_inactive_type()
    {
        using var db = _factory.CreateContext();
        var property = new Property { Name = "P2", Address = "A2" };
        var inactiveType = new UnitType { Name = "LoftKeep", IsActive = false };
        db.Properties.Add(property);
        db.UnitTypes.Add(inactiveType);
        await db.SaveChangesAsync();

        var unit = new Unit { PropertyId = property.Id, UnitNumber = "2A", Bedrooms = 1, MonthlyRent = 900m, UnitTypeId = inactiveType.Id };
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        var service = new UnitService(db);

        var act = () => service.UpdateAsync(unit.Id, "2A", 1, 950m, inactiveType.Id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateAsync_rejects_switching_to_a_different_inactive_type()
    {
        using var db = _factory.CreateContext();
        var property = new Property { Name = "P3", Address = "A3" };
        var activeType = new UnitType { Name = "StudioSwitch", IsActive = true };
        var inactiveType = new UnitType { Name = "LoftSwitch", IsActive = false };
        db.Properties.Add(property);
        db.UnitTypes.AddRange(activeType, inactiveType);
        await db.SaveChangesAsync();

        var unit = new Unit { PropertyId = property.Id, UnitNumber = "3A", Bedrooms = 1, MonthlyRent = 900m, UnitTypeId = activeType.Id };
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        var service = new UnitService(db);

        var act = () => service.UpdateAsync(unit.Id, "3A", 1, 950m, inactiveType.Id);

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task IsAvailableAsync_is_false_when_a_lease_covers_the_date()
    {
        using var db = _factory.CreateContext();
        var property = new Property { Name = "P4", Address = "A4" };
        var type = new UnitType { Name = "Studio2", IsActive = true };
        db.Properties.Add(property);
        db.UnitTypes.Add(type);
        await db.SaveChangesAsync();

        var unit = new Unit { PropertyId = property.Id, UnitNumber = "4A", Bedrooms = 1, MonthlyRent = 900m, UnitTypeId = type.Id };
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        var application = new RentalApplication { UnitId = unit.Id, Status = RentalManagement.Domain.Enums.ApplicationStatus.Approved };
        db.RentalApplications.Add(application);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Leases.Add(new Lease { UnitId = unit.Id, RentalApplicationId = application.Id, StartDate = today.AddMonths(-1), EndDate = today.AddMonths(11) });
        await db.SaveChangesAsync();

        var service = new UnitService(db);

        (await service.IsAvailableAsync(unit.Id, today)).Should().BeFalse();
        (await service.IsAvailableAsync(unit.Id, today.AddMonths(12))).Should().BeTrue();
    }
}
