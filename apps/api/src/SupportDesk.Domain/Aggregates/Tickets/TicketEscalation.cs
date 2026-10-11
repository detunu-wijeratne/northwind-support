
namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Immutable audit record of a ticket escalation.
/// </summary>
public sealed class TicketEscalation
{
    private TicketEscalation()
    {
        // Required by EF Core.
    }

    public TicketEscalation(
        int ticketId,
        TicketPriority fromPriority,
        TicketPriority toPriority,
        int? fromAgentId,
        int? toAgentId,
        DateTime? fromDueAtUtc,
        DateTime? toDueAtUtc,
        string reason,
        string escalatedBy,
        DateTime escalatedAtUtc)
    {
        if (ticketId <= 0)
            throw new ArgumentOutOfRangeException(nameof(ticketId));

        if (string.IsNullOrWhiteSpace(reason) ||
            reason.Trim().Length < 5 ||
            reason.Trim().Length > 500)
            throw new ArgumentException(
                "Escalation reason must contain 5 to 500 characters.",
                nameof(reason));

        if (string.IsNullOrWhiteSpace(escalatedBy) ||
            escalatedBy.Trim().Length > 100)
            throw new ArgumentException(
                "EscalatedBy must contain 1 to 100 characters.",
                nameof(escalatedBy));

        TicketId = ticketId;
        FromPriority = fromPriority;
        ToPriority = toPriority;
        FromAgentId = fromAgentId;
        ToAgentId = toAgentId;
        FromDueAtUtc = fromDueAtUtc;
        ToDueAtUtc = toDueAtUtc;
        Reason = reason.Trim();
        EscalatedBy = escalatedBy.Trim();
        EscalatedAtUtc = escalatedAtUtc;
    }

    public int Id { get; private set; }

    public int TicketId { get; private set; }

    public TicketPriority FromPriority { get; private set; }

    public TicketPriority ToPriority { get; private set; }

    public int? FromAgentId { get; private set; }

    public int? ToAgentId { get; private set; }

    public DateTime? FromDueAtUtc { get; private set; }

    public DateTime? ToDueAtUtc { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string EscalatedBy { get; private set; } = string.Empty;

    public DateTime EscalatedAtUtc { get; private set; }
}
