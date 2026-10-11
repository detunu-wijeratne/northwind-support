
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentAssignmentQuery agentAssignment,
    IUnitOfWork unitOfWork,
    IClock clock,
    SlaCalculator slaCalculator,
    GetTicketQueryHandler getTicket)
{
    public async Task<EscalateTicketResponse> HandleAsync(
        int id,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        var actor = request.EscalatedBy?.Trim();

        if (string.IsNullOrWhiteSpace(reason) ||
            reason.Length < 5 ||
            reason.Length > 500)
        {
            throw new ArgumentException(
                "Escalation reason must be between 5 and 500 characters.");
        }

        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 100)
        {
            throw new ArgumentException(
                "EscalatedBy is required and must be at most 100 characters.");
        }

        var ticket = await tickets.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Ticket", id);

        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            throw new ConflictException(
                $"Ticket {ticket.Reference} cannot be escalated because it is {ticket.Status}.");
        }

        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new ConflictException(
                $"Ticket {ticket.Reference} cannot be escalated because it is already Critical.");
        }

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var now = clock.UtcNow;
        var nextPriority = (TicketPriority)((int)ticket.Priority + 1);
        var newDueAt = slaCalculator.CalculateDueDate(
            nextPriority,
            customer.Tier,
            now);

        var newAgentId = ticket.AssignedAgentId;

        if (!newAgentId.HasValue ||
            !await agentAssignment.CanKeepAssignmentAsync(
                newAgentId.Value,
                category.Id,
                category.RequiresSpecialist,
                ct))
        {
            newAgentId = await agentAssignment.FindBestAgentIdAsync(
                category.Id,
                category.RequiresSpecialist,
                ct);
        }

        var history = new TicketEscalation(
            ticket.Id,
            ticket.Priority,
            nextPriority,
            ticket.AssignedAgentId,
            newAgentId,
            ticket.DueAtUtc,
            newDueAt,
            reason,
            actor,
            now);

        ticket.Escalate(newDueAt, newAgentId, now);

        await tickets.AddEscalationAsync(history, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new EscalateTicketResponse
        {
            Ticket = await getTicket.HandleAsync(id, ct),
            Escalation = EscalationDto.From(history)
        };
    }
}
