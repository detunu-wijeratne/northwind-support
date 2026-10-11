namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Request to increase a ticket's priority by one level.
/// </summary>
public sealed class EscalateTicketRequest
{
    public string Reason { get; set; } = string.Empty;

    // Task 3 will replace this with the authenticated agent.
    public string EscalatedBy { get; set; } = string.Empty;
}
