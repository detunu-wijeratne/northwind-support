
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

/// <summary>
/// Loads and stores ticket aggregates and escalation history.
/// </summary>
public sealed class TicketRepository(SupportDbContext db)
    : ITicketRepository
{
    public Task<Ticket?> GetByIdAsync(
        int id,
        CancellationToken ct) =>
        db.Set<Ticket>()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<string> NextReferenceAsync(
        CancellationToken ct)
    {
        var lastId = await db.Set<Ticket>()
            .AsNoTracking()
            .OrderByDescending(t => t.Id)
            .Select(t => t.Id)
            .FirstOrDefaultAsync(ct);

        return $"TCK-{lastId + 1:D4}";
    }

    public async Task AddAsync(
        Ticket ticket,
        CancellationToken ct)
    {
        await db.Set<Ticket>().AddAsync(ticket, ct);
    }

    /// <summary>
    /// Stages an escalation history record.
    /// SaveChanges is handled by IUnitOfWork.
    /// </summary>
    public async Task AddEscalationAsync(
        TicketEscalation escalation,
        CancellationToken ct)
    {
        await db.TicketEscalations.AddAsync(escalation, ct);
    }

    /// <summary>
    /// Retrieves escalation history newest first.
    /// </summary>
    public async Task<IReadOnlyList<TicketEscalation>>
        GetEscalationsAsync(
            int ticketId,
            CancellationToken ct)
    {
        return await db.TicketEscalations
            .AsNoTracking()
            .Where(e => e.TicketId == ticketId)
            .OrderByDescending(e => e.EscalatedAtUtc)
            .ThenByDescending(e => e.Id)
            .ToListAsync(ct);
    }
}
