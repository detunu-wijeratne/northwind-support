namespace SupportDesk.Application.Contracts.Tickets;

public sealed class EscalateTicketResponse
{
    public TicketDetailDto Ticket { get; set; } = null!;

    public EscalationDto Escalation { get; set; } = null!;
}