namespace RentalManagement.Web.Models.Applications;

public class ConfirmDeleteResidenceViewModel
{
    public int ApplicationId { get; set; }
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
