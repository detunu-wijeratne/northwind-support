
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Services;

public sealed class SlaCalculatorTests
{
    private readonly SlaCalculator _calculator = new(new SlaOptions
    {
        BaseWindowHours = new Dictionary<string, double>
        {
            ["Critical"] = 4,
            ["High"] = 8,
            ["Medium"] = 24,
            ["Low"] = 72
        },
        PremiumCustomerMultiplier = 0.5,
        MinimumWindowHours = 1,
        AtRiskThresholdPercent = 25
    });

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void StandardCustomer_UsesBaseWindow(
        TicketPriority priority,
        double expectedHours)
    {
        var window = _calculator.GetWindow(
            priority,
            CustomerTier.Standard);

        Assert.Equal(expectedHours, window.TotalHours);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, 2)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.Low, 36)]
    public void PremiumCustomer_GetsHalfWindow(
        TicketPriority priority,
        double expectedHours)
    {
        var window = _calculator.GetWindow(
            priority,
            CustomerTier.Premium);

        Assert.Equal(expectedHours, window.TotalHours);
    }

    [Fact]
    public void PremiumWindow_NeverFallsBelowMinimum()
    {
        var calculator = new SlaCalculator(new SlaOptions
        {
            BaseWindowHours = new Dictionary<string, double>
            {
                ["Critical"] = 1
            },
            PremiumCustomerMultiplier = 0.5,
            MinimumWindowHours = 1
        });

        var window = calculator.GetWindow(
            TicketPriority.Critical,
            CustomerTier.Premium);

        Assert.Equal(1, window.TotalHours);
    }

    [Fact]
    public void DueDate_IsCalculatedFromStartTime()
    {
        var start = new DateTime(
            2026, 10, 11, 10, 0, 0, DateTimeKind.Utc);

        var due = _calculator.CalculateDueDate(
            TicketPriority.High,
            CustomerTier.Premium,
            start);

        Assert.Equal(start.AddHours(4), due);
    }
}
