
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Applies agent eligibility and workload rules using SQL queries.
/// </summary>
public sealed class AgentAssignmentQuery(SupportDbContext db)
    : IAgentAssignmentQuery
{
    public async Task<int?> FindBestAgentIdAsync(
        int categoryId,
        bool requiresSpecialist,
        CancellationToken ct)
    {
        var agents = GetEligibleAgents(
            categoryId,
            requiresSpecialist);

        return await agents
            .OrderBy(agent => agent.OpenTicketCount)
            .ThenBy(agent => agent.Id)
            .Select(agent => (int?)agent.Id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> CanKeepAssignmentAsync(
        int agentId,
        int categoryId,
        bool requiresSpecialist,
        CancellationToken ct)
    {
        // An agent holding this ticket already uses one
        // capacity slot. Keeping it doesn't add another.
        return GetEligibleAgents(
                categoryId,
                requiresSpecialist,
                allowAtCapacity: true)
            .AnyAsync(agent => agent.Id == agentId, ct);
    }

    private IQueryable<AgentCandidate> GetEligibleAgents(
        int categoryId,
        bool requiresSpecialist,
        bool allowAtCapacity = false)
    {
        var agents = db.Set<Agent>()
            .AsNoTracking()
            .Where(agent => agent.IsActive);

        if (requiresSpecialist)
        {
            agents = agents.Where(agent =>
                agent.Specializations.Any(
                    s => s.CategoryId == categoryId));
        }

        var candidates = agents.Select(agent =>
            new AgentCandidate
            {
                Id = agent.Id,
                MaxOpenTickets = agent.MaxOpenTickets,
                OpenTicketCount = db.Set<Ticket>()
                    .Count(ticket =>
                        ticket.AssignedAgentId == agent.Id &&
                        ticket.Status != TicketStatus.Resolved &&
                        ticket.Status != TicketStatus.Closed)
            });

        return allowAtCapacity
            ? candidates.Where(agent =>
                agent.OpenTicketCount <= agent.MaxOpenTickets)
            : candidates.Where(agent =>
                agent.OpenTicketCount < agent.MaxOpenTickets);
    }

    private sealed class AgentCandidate
    {
        public int Id { get; set; }

        public int MaxOpenTickets { get; set; }

        public int OpenTicketCount { get; set; }
    }
}
