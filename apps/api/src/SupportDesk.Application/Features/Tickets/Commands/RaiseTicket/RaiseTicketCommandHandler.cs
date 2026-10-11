using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket and automatically determines priority,
/// SLA deadline and eligible agent.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    TicketTriageService triageService,
    ILogger<RaiseTicketCommandHandler> logger)
{
    public async Task<TicketDetailDto> HandleAsync(
        CreateTicketRequest request,
        CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(
            request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var category = await categories.GetByIdAsync(
            request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var nowUtc = clock.UtcNow;

        var triage = await triageService.TriageAsync(
            request.RequestedPriority ?? TicketPriority.Medium,
            customer,
            category,
            nowUtc,
            ct);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            triage.Priority,
            nowUtc);

        ticket.SetDueDate(triage.DueAtUtc, nowUtc);
        ticket.AssignTo(triage.AgentId, nowUtc);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} created with priority {Priority}, deadline {DueAtUtc} and agent {AgentId}.",
            ticket.Reference,
            ticket.Priority,
            ticket.DueAtUtc,
            ticket.AssignedAgentId);

        var response = await getTicket.HandleAsync(ticket.Id, ct);
        response.Triage = triage.Explanation;

        return response;
    }
}
