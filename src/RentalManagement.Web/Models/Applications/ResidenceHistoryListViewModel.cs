using RentalManagement.Domain.Entities;

namespace RentalManagement.Web.Models.Applications;

public class ResidenceHistoryListViewModel
{
    public int ApplicationId { get; set; }
    public bool IsEditable { get; set; }
    public int ResidenceHistoryVersion { get; set; }
    public List<ResidenceHistoryEntry> Entries { get; set; } = [];
}
