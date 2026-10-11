namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Request to increase a ticket's priority by one level.
/// </summary>
public sealed class EscalateTicketRequest
{
    public string Reason { get; set; } = string.Empty;

    // Set by the API from the authenticated agent's JWT, never from client input.
    public string EscalatedBy { get; set; } = string.Empty;
}
