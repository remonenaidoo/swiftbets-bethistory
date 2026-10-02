using System.ComponentModel.DataAnnotations;

namespace SwiftBets.History.Infrastructure.Integrity;

public sealed class IntegrityClients
{
    public const string SectionName = "Integrity:Clients";

    [Required]
    public string PlacementAddress { get; set; } = string.Empty;

    [Required]
    public string SettlementAddress { get; set; } = string.Empty;

    [Required]
    public string PayoutAddress { get; set; } = string.Empty;
}
