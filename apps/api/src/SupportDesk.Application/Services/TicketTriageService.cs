
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services;

/// <summary>
/// Applies automatic priority, SLA and assignment rules.
/// Reusable for ticket creation and escalation.
/// </summary>
public sealed class TicketTriageService(
    SlaCalculator slaCalculator,
    IAgentAssignmentQuery agentAssignmentQuery)
{
    public async Task<TriageResult> TriageAsync(
        TicketPriority requestedPriority,
        Customer customer,
        Category category,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var priority = category.ForcesCriticalPriority
            ? TicketPriority.Critical
            : requestedPriority;

        var window = slaCalculator.GetWindow(priority, customer.Tier);
        var dueAtUtc = nowUtc.Add(window);

        var agentId = await agentAssignmentQuery.FindBestAgentIdAsync(
            category.Id,
            category.RequiresSpecialist,
            ct);

        var explanation = new TriageDecisionDto
        {
            PriorityReason = category.ForcesCriticalPriority
                ? $"Category {category.Name} requires Critical priority."
                : $"Requested priority {priority} was applied.",

            SlaReason =
                $"{priority} priority, {customer.Tier} customer: " +
                $"{window.TotalHours:0.##}-hour response window.",

            AssignmentReason = agentId.HasValue
                ? $"Assigned to eligible agent #{agentId.Value} with the lowest open-ticket workload."
                : "No eligible agent is currently active, specialised and below capacity. Ticket remains unassigned."
        };

        return new TriageResult(
            priority,
            dueAtUtc,
            agentId,
            explanation);
    }
}

/// <summary>
/// Result of automatic ticket triage.
/// </summary>
public sealed record TriageResult(
    TicketPriority Priority,
    DateTime DueAtUtc,
    int? AgentId,
    TriageDecisionDto Explanation);
