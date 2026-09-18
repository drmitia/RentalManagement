namespace RentalManagement.Domain.Services.Models;

public record ApplicantInfoInput(string FullName, string Phone, string Email, string CurrentAddress, byte[]? ExpectedRowVersion);
