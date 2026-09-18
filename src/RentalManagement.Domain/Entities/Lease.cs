namespace RentalManagement.Domain.Entities;

public class Lease
{
    public int Id { get; set; }

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool CoversDate(DateOnly date) => date >= StartDate && date <= EndDate;
}
