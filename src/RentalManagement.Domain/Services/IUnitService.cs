using RentalManagement.Domain.Entities;

namespace RentalManagement.Domain.Services;

public interface IUnitService
{
    Task<List<Unit>> GetByPropertyIdAsync(int propertyId);
    Task<List<Unit>> GetAvailableAsync();
    Task<List<UnitType>> GetUnitTypesAsync();
    Task<Unit?> GetByIdAsync(int id);
    Task<Unit> CreateAsync(int propertyId, string unitNumber, int bedrooms, decimal monthlyRent, int unitTypeId);
    Task UpdateAsync(int id, string unitNumber, int bedrooms, decimal monthlyRent, int unitTypeId);
    Task DeleteAsync(int id);
    Task<bool> IsAvailableAsync(int unitId, DateOnly asOf);
}
