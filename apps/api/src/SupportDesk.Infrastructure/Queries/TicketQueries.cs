using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked.
/// Aggregates hold only each other's ids, so the customer,
/// category and agent labels are joined on the read side.
/// </summary>
public sealed class TicketQueries(SupportDbContext db, IClock clock) : ITicketQueries
{
    /// <remarks>
    /// Applies all supplied ticket filters in SQL before counting,
    /// sorting and pagination. Filters combine using AND.
    /// </remarks>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(
        TicketQuery query,
        CancellationToken ct)
    {
        // Apply filters before counting or pagination.
        var tickets = ApplyFilters(TicketsWithLabels(), query);

        // Count only tickets matching the supplied filters.
        var totalCount = await tickets.CountAsync(ct);

        var now = clock.UtcNow;

        // Sort, paginate and project inside the database.
        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(
                    x.Customer.Id,
                    x.Customer.Name,
                    x.Customer.Tier),
                Category = new CategorySummaryDto(
                    x.Category.Id,
                    x.Category.Name),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(
                        x.Agent.Id,
                        x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(
            items,
            query.Page,
            query.PageSize,
            totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(
        int id,
        CancellationToken ct)
    {
        var now = clock.UtcNow;

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id,
                    x.Customer.Name,
                    x.Customer.Email,
                    x.Customer.Phone,
                    x.Customer.Tier),
                Category = new CategoryDto(
                    x.Category.Id,
                    x.Category.Name,
                    x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(
                        x.Agent.Id,
                        x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now)
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(
        int customerId,
        CancellationToken ct)
    {
        var now = clock.UtcNow;

        return await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(
                    x.Customer.Id,
                    x.Customer.Name,
                    x.Customer.Tier),
                Category = new CategorySummaryDto(
                    x.Category.Id,
                    x.Category.Name),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(
                        x.Agent.Id,
                        x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now)
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Every ticket with the related rows needed for display.
    /// Filtering, sorting and projection are performed in SQL.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>()
            on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>()
            on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>()
            .Where(a => a.Id == ticket.AssignedAgentId)
            .DefaultIfEmpty()
        select new TicketWithLabels
        {
            Ticket = ticket,
            Customer = customer,
            Category = category,
            Agent = agent
        };

    /// <summary>
    /// Applies optional filters to the IQueryable.
    /// Each Where adds an AND condition.
    /// The resulting query is executed by SQL Server.
    /// </summary>
    private static IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source,
        TicketQuery query)
    {
        // FL-1: Exact-match status
        if (query.Status.HasValue)
        {
            source = source.Where(x =>
                x.Ticket.Status == query.Status.Value);
        }

        // FL-1: Exact-match priority
        if (query.Priority.HasValue)
        {
            source = source.Where(x =>
                x.Ticket.Priority == query.Priority.Value);
        }

        // FL-1: Exact-match category
        if (query.CategoryId.HasValue)
        {
            source = source.Where(x =>
                x.Ticket.CategoryId == query.CategoryId.Value);
        }

        // FL-1: Exact-match customer
        if (query.CustomerId.HasValue)
        {
            source = source.Where(x =>
                x.Ticket.CustomerId == query.CustomerId.Value);
        }

        // FL-1: Exact-match assigned agent
        if (query.AssignedAgentId.HasValue)
        {
            source = source.Where(x =>
                x.Ticket.AssignedAgentId == query.AssignedAgentId.Value);
        }

        // FL-2: Unassigned tickets only
        if (query.UnassignedOnly)
        {
            source = source.Where(x =>
                x.Ticket.AssignedAgentId == null);
        }

        // FL-3: Search ticket title, reference or customer name
        var search = query.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            source = source.Where(x =>
                x.Ticket.Title.Contains(search) ||
                x.Ticket.Reference.Contains(search) ||
                x.Customer.Name.Contains(search));
        }

        return source;
    }

    /// <summary>
    /// Applies the requested sorting.
    /// The ticket ID provides deterministic pagination.
    /// </summary>
    private static IQueryable<TicketWithLabels> ApplySort(
        IQueryable<TicketWithLabels> source,
        TicketQuery query)
    {
        var descending = !string.Equals(
            query.SortDirection,
            "asc",
            StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),

            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),

            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),

            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),

            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Stable paging when sorting values are identical.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }
    }
}
