
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads and stores ticket aggregates and their escalation history.
/// Changes are committed through IUnitOfWork.
/// </summary>
public interface ITicketRepository
{
    /// <summary>
    /// Loads a tracked ticket for modification.
    /// </summary>
    Task<Ticket?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Generates the next ticket reference.
    /// </summary>
    Task<string> NextReferenceAsync(CancellationToken ct);

    /// <summary>
    /// Adds a new ticket.
    /// </summary>
    Task AddAsync(Ticket ticket, CancellationToken ct);

    /// <summary>
    /// Adds an immutable escalation history record.
    /// The caller commits through IUnitOfWork.
    /// </summary>
    Task AddEscalationAsync(
        TicketEscalation escalation,
        CancellationToken ct);

    /// <summary>
    /// Returns a ticket's escalation history,
    /// newest first.
    /// </summary>
    Task<IReadOnlyList<TicketEscalation>> GetEscalationsAsync(
        int ticketId,
        CancellationToken ct);
}
