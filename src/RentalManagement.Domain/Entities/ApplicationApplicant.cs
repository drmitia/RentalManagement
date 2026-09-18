namespace RentalManagement.Domain.Entities;

public class ApplicationApplicant
{
    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
}
