namespace RentalManagement.Domain.Entities;

public class ApplicantInfo
{
    public int Id { get; set; }

    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CurrentAddress { get; set; } = string.Empty;

    public byte[]? RowVersion { get; set; }
}
