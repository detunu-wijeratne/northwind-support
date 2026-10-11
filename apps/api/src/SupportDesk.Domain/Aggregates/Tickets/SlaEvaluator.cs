
namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Derives SLA status from ticket timestamps.
/// SLA status is never stored in the database.
/// </summary>
public static class SlaEvaluator
{
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        DateTime? windowStartUtc = null,
        double atRiskThresholdPercent = 25)
    {
        if (dueAtUtc is null)
            return SlaStatus.NotApplicable;

        if (resolvedAtUtc is not null)
            return resolvedAtUtc <= dueAtUtc
                ? SlaStatus.Met
                : SlaStatus.Breached;

        if (nowUtc > dueAtUtc)
            return SlaStatus.Breached;

        if (windowStartUtc is null)
            return SlaStatus.WithinSla;

        var totalWindow = dueAtUtc.Value - windowStartUtc.Value;

        if (totalWindow <= TimeSpan.Zero)
            return SlaStatus.WithinSla;

        var remaining = dueAtUtc.Value - nowUtc;

        var threshold = totalWindow.TotalSeconds *
                        (atRiskThresholdPercent / 100.0);

        return remaining.TotalSeconds <= threshold
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }
}
