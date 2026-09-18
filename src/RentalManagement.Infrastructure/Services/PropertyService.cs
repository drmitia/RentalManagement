using Microsoft.EntityFrameworkCore;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Exceptions;
using RentalManagement.Domain.Services;
using RentalManagement.Infrastructure.Persistence;

namespace RentalManagement.Infrastructure.Services;

public class PropertyService(AppDbContext db) : IPropertyService
{
    public Task<List<Property>> GetAllWithUnitsAsync() =>
        db.Properties
            .Include(p => p.Units).ThenInclude(u => u.UnitType)
            .OrderBy(p => p.Name)
            .ToListAsync();

    public Task<List<Property>> GetLookupListAsync() =>
        db.Properties.OrderBy(p => p.Name).ToListAsync();

    public Task<Property?> GetByIdAsync(int id) =>
        db.Properties.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Property> CreateAsync(string name, string address)
    {
        var property = new Property { Name = name, Address = address };
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        return property;
    }

    public async Task UpdateAsync(int id, string name, string address)
    {
        var property = await db.Properties.FindAsync(id)
            ?? throw new DomainValidationException("Property not found.");

        property.Name = name;
        property.Address = address;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var property = await db.Properties.FindAsync(id)
            ?? throw new DomainValidationException("Property not found.");

        var hasApplications = await db.RentalApplications.AnyAsync(a => a.Unit.PropertyId == id);
        if (hasApplications)
        {
            throw new DomainValidationException("This property has units with rental applications and cannot be removed.");
        }

        db.Properties.Remove(property);
        await db.SaveChangesAsync();
    }
}
