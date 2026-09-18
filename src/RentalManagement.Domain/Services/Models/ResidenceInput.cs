namespace RentalManagement.Domain.Services.Models;

public record ResidenceInput(string Address, string LandlordName, string LandlordPhone, DateOnly MoveInDate, DateOnly? MoveOutDate);
