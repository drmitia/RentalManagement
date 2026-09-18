using Microsoft.EntityFrameworkCore;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Exceptions;
using RentalManagement.Domain.Services;
using RentalManagement.Infrastructure.Persistence;
using Unit = RentalManagement.Domain.Entities.Unit;

namespace RentalManagement.Infrastructure.Services;

public class UnitService(AppDbContext db) : IUnitService
{
    public Task<List<Unit>> GetByPropertyIdAsync(int propertyId) =>
        db.Units
            .Where(u => u.PropertyId == propertyId)
            .Include(u => u.UnitType)
            .OrderBy(u => u.UnitNumber)
            .ToListAsync();

    public async Task<List<Unit>> GetAvailableAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await db.Units
            .Include(u => u.Property)
            .Include(u => u.UnitType)
            .Where(u => !db.Leases.Any(l => l.UnitId == u.Id && l.StartDate <= today && l.EndDate >= today))
            .OrderBy(u => u.Property.Name).ThenBy(u => u.UnitNumber)
            .ToListAsync();
    }

    public Task<List<UnitType>> GetUnitTypesAsync() =>
        db.UnitTypes.OrderBy(t => t.Name).ToListAsync();

    public Task<Unit?> GetByIdAsync(int id) =>
        db.Units.Include(u => u.UnitType).FirstOrDefaultAsync(u => u.Id == id);

    public async Task<Unit> CreateAsync(int propertyId, string unitNumber, int bedrooms, decimal monthlyRent, int unitTypeId)
    {
        await EnsureUnitTypeSelectableAsync(unitTypeId, currentUnitTypeId: null);

        var unit = new Unit
        {
            PropertyId = propertyId,
            UnitNumber = unitNumber,
            Bedrooms = bedrooms,
            MonthlyRent = monthlyRent,
            UnitTypeId = unitTypeId
        };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        return unit;
    }

    public async Task UpdateAsync(int id, string unitNumber, int bedrooms, decimal monthlyRent, int unitTypeId)
    {
        var unit = await db.Units.FindAsync(id)
            ?? throw new DomainValidationException("Unit not found.");

        await EnsureUnitTypeSelectableAsync(unitTypeId, currentUnitTypeId: unit.UnitTypeId);

        unit.UnitNumber = unitNumber;
        unit.Bedrooms = bedrooms;
        unit.MonthlyRent = monthlyRent;
        unit.UnitTypeId = unitTypeId;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var unit = await db.Units.FindAsync(id)
            ?? throw new DomainValidationException("Unit not found.");

        var hasApplications = await db.RentalApplications.AnyAsync(a => a.UnitId == id);
        if (hasApplications)
        {
            throw new DomainValidationException("This unit has rental applications and cannot be removed.");
        }

        db.Units.Remove(unit);
        await db.SaveChangesAsync();
    }

    public async Task<bool> IsAvailableAsync(int unitId, DateOnly asOf) =>
        !await db.Leases.AnyAsync(l => l.UnitId == unitId && l.StartDate <= asOf && l.EndDate >= asOf);

    private async Task EnsureUnitTypeSelectableAsync(int unitTypeId, int? currentUnitTypeId)
    {
        if (unitTypeId == currentUnitTypeId)
        {
            return;
        }

        var unitType = await db.UnitTypes.FindAsync(unitTypeId)
            ?? throw new DomainValidationException("Unit type not found.", nameof(unitTypeId));

        if (!unitType.IsActive)
        {
            throw new DomainValidationException("This unit type is inactive and cannot be selected.", nameof(unitTypeId));
        }
    }
}
