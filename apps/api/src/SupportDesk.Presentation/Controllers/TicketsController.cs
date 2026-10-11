using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Queries.SearchTickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Domain.Repositories;
using System.Security.Claims;


namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
public sealed class TicketsController : ControllerBase
{
    /// <summary>Searches tickets with filters, sorting and paging.</summary>
    [HttpGet]
    public Task<PagedResult<TicketListItemDto>> Search(
        [FromQuery] TicketQuery query,
        [FromServices] SearchTicketsQueryHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(query, ct);

    /// <summary>Returns one ticket.</summary>
    [HttpGet("{id:int}")]
    public Task<TicketDetailDto> Get(int id, [FromServices] GetTicketQueryHandler handler, CancellationToken ct) =>
        handler.HandleAsync(id, ct);

    /// <summary>Raises a new ticket.</summary>
    [HttpPost]
    public async Task<ActionResult<TicketDetailDto>> Create(
        CreateTicketRequest request,
        [FromServices] RaiseTicketCommandHandler handler,
        CancellationToken ct)
    {
        var created = await handler.HandleAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    
    /// <summary>
    /// Escalates an open, non-Critical ticket by one priority level.
    /// </summary>
    [HttpPost("{id:int}/escalate")]
    public async Task<ActionResult<EscalateTicketResponse>> Escalate(
        int id,
        [FromBody] EscalationRequest request,
        [FromServices] EscalateTicketCommandHandler handler,
        CancellationToken ct)
    {
        var agentName = User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(agentName))
        {
            return Unauthorized();
        }

        var command = new EscalateTicketRequest
        {
            Reason = request.Reason,
            EscalatedBy = agentName
        };

        var result = await handler.HandleAsync(id, command, ct);

        return Ok(result);
    }



    /// <summary>Moves a ticket to another status.</summary>
    [HttpPatch("{id:int}/status")]
    public Task<TicketDetailDto> UpdateStatus(
        int id,
        UpdateTicketStatusRequest request,
        [FromServices] ChangeTicketStatusCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, request, ct);

    /// <summary>Assigns the ticket to an agent, or unassigns it when no agent is given.</summary>
    [HttpPatch("{id:int}/assignment")]
    public Task<TicketDetailDto> Assign(
        int id,
        AssignTicketRequest request,
        [FromServices] AssignTicketCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, request, ct);

    
    /// <summary>
    /// Returns the escalation history for a ticket, newest first.
    /// </summary>
    [HttpGet("{id:int}/escalations")]
    public async Task<IReadOnlyList<EscalationDto>> GetEscalations(
        int id,
        [FromServices] GetTicketQueryHandler getTicket,
        [FromServices] ITicketRepository tickets,
        CancellationToken ct)
    {
        // Verify the ticket exists so an unknown ID returns 404.
        await getTicket.HandleAsync(id, ct);

        var history = await tickets.GetEscalationsAsync(id, ct);

        return history.Select(EscalationDto.From).ToList();
    }

public sealed record EscalationRequest(string Reason);


}
