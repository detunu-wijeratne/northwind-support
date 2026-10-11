
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Services;

public sealed class TicketTriageServiceTests
{
    private static readonly DateTime Now =
        new(2026, 10, 11, 10, 0, 0, DateTimeKind.Utc);

    private static TicketTriageService CreateService(
        int? selectedAgentId = null)
    {
        var options = new SlaOptions
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
        };

        var agentQuery = new Mock<IAgentAssignmentQuery>();

        agentQuery
            .Setup(x => x.FindBestAgentIdAsync(
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(selectedAgentId);

        return new TicketTriageService(
            new SlaCalculator(options),
            agentQuery.Object);
    }

    [Fact]
    public async Task ForcedCriticalCategory_OverridesRequestedPriority()
    {
        var customer = CreateCustomer(CustomerTier.Standard);
        var category = CreateCategory(forcesCritical: true);
        var service = CreateService();

        var result = await service.TriageAsync(
            TicketPriority.Low, customer, category,
            Now, CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, result.Priority);
        Assert.Equal(Now.AddHours(4), result.DueAtUtc);
        Assert.Contains("Critical", result.Explanation.PriorityReason);
    }

    [Fact]
    public async Task PremiumCustomer_GetsReducedDeadline()
    {
        var customer = CreateCustomer(CustomerTier.Premium);
        var category = CreateCategory();
        var service = CreateService();

        var result = await service.TriageAsync(
            TicketPriority.High, customer, category,
            Now, CancellationToken.None);

        Assert.Equal(TicketPriority.High, result.Priority);
        Assert.Equal(Now.AddHours(4), result.DueAtUtc);
    }

    [Fact]
    public async Task EligibleAgent_IsSelected()
    {
        var service = CreateService(selectedAgentId: 2);

        var result = await service.TriageAsync(
            TicketPriority.Medium,
            CreateCustomer(CustomerTier.Standard),
            CreateCategory(),
            Now,
            CancellationToken.None);

        Assert.Equal(2, result.AgentId);
        Assert.Contains("Assigned", result.Explanation.AssignmentReason);
    }

    [Fact]
    public async Task NoEligibleAgent_ReturnsUnassignedWithReason()
    {
        var service = CreateService();

        var result = await service.TriageAsync(
            TicketPriority.Medium,
            CreateCustomer(CustomerTier.Standard),
            CreateCategory(),
            Now,
            CancellationToken.None);

        Assert.Null(result.AgentId);
        Assert.Contains("No eligible agent", result.Explanation.AssignmentReason);
    }

    private static Customer CreateCustomer(CustomerTier tier) =>
        new("Example Customer", "customer@example.test", null, tier, Now);

    private static Category CreateCategory(bool forcesCritical = false) =>
        new("Example Category",
            requiresSpecialist: false,
            forcesCriticalPriority: forcesCritical);
}
