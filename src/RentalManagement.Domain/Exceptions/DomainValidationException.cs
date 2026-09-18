namespace RentalManagement.Domain.Exceptions;

public class DomainValidationException(string message, string? field = null) : Exception(message)
{
    public string? Field { get; } = field;
}
