namespace SupportDesk.Application.Services;

/// <summary>
/// Configurable SLA rules loaded from the "Sla" section.
/// </summary>
public sealed class SlaOptions
{
    public Dictionary<string, double> BaseWindowHours { get; set; } = new();

    public double PremiumCustomerMultiplier { get; set; } = 0.5;

    public double MinimumWindowHours { get; set; } = 1;

    public double AtRiskThresholdPercent { get; set; } = 25;
}
