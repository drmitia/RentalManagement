using RentalManagement.Domain.Entities;

namespace RentalManagement.Domain.Services;

public interface IPropertyService
{
    Task<List<Property>> GetAllWithUnitsAsync();
    Task<List<Property>> GetLookupListAsync();
    Task<Property?> GetByIdAsync(int id);
    Task<Property> CreateAsync(string name, string address);
    Task UpdateAsync(int id, string name, string address);
    Task DeleteAsync(int id);
}
