
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services;

/// <summary>
/// Calculates configurable SLA deadlines.
/// </summary>
public sealed class SlaCalculator(SlaOptions config)
{
    public TimeSpan GetWindow(
        TicketPriority priority,
        CustomerTier customerTier)
    {
        if (!config.BaseWindowHours.TryGetValue(
                priority.ToString(),
                out var baseHours))
        {
            throw new InvalidOperationException(
                $"Missing SLA window for priority {priority}.");
        }

        var hours = baseHours;

        if (customerTier == CustomerTier.Premium)
        {
            hours *= config.PremiumCustomerMultiplier;
        }

        hours = Math.Max(hours, config.MinimumWindowHours);

        return TimeSpan.FromHours(hours);
    }

    public DateTime CalculateDueDate(
        TicketPriority priority,
        CustomerTier customerTier,
        DateTime startUtc)
    {
        return startUtc.Add(GetWindow(priority, customerTier));
    }
}
