using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// No-tracking ticket reads. Filtering, counting, sorting and paging happen in SQL.
/// SLA display values are evaluated only after the relevant rows are retrieved.
/// </summary>
public sealed class TicketQueries(
    SupportDbContext db,
    IClock clock,
    SlaOptions? slaOptions = null) : ITicketQueries
{
    private double AtRiskPercent => slaOptions?.AtRiskThresholdPercent ?? 25.0;

    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(
        TicketQuery query,
        CancellationToken ct)
    {
        query = query.Normalized();
        var now = clock.UtcNow;
        var tickets = ApplyFilters(TicketsWithLabels(), query, now);
        var totalCount = await tickets.CountAsync(ct);

        var rows = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new
            {
                Item = new TicketListItemDto
                {
                    Id = x.Ticket.Id,
                    Reference = x.Ticket.Reference,
                    Title = x.Ticket.Title,
                    Status = x.Ticket.Status,
                    Priority = x.Ticket.Priority,
                    Customer = new CustomerSummaryDto(
                        x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                    Category = new CategorySummaryDto(
                        x.Category.Id, x.Category.Name),
                    AssignedAgent = x.Agent == null
                        ? null
                        : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                    CreatedAtUtc = x.Ticket.CreatedAtUtc,
                    UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                    DueAtUtc = x.Ticket.DueAtUtc,
                    ResolvedAtUtc = x.Ticket.ResolvedAtUtc
                },
                WindowStartUtc = db.Set<TicketEscalation>()
                    .Where(e => e.TicketId == x.Ticket.Id)
                    .OrderByDescending(e => e.EscalatedAtUtc)
                    .ThenByDescending(e => e.Id)
                    .Select(e => (DateTime?)e.EscalatedAtUtc)
                    .FirstOrDefault() ?? x.Ticket.CreatedAtUtc
            })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            row.Item.SlaStatus = SlaEvaluator.Evaluate(
                row.Item.DueAtUtc,
                row.Item.ResolvedAtUtc,
                now,
                row.WindowStartUtc,
                AtRiskPercent);
        }

        return new PagedResult<TicketListItemDto>(
            rows.Select(row => row.Item).ToList(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var row = await TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new
            {
                Item = new TicketDetailDto
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
                        : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                    CreatedAtUtc = x.Ticket.CreatedAtUtc,
                    UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                    DueAtUtc = x.Ticket.DueAtUtc,
                    ResolvedAtUtc = x.Ticket.ResolvedAtUtc
                },
                WindowStartUtc = db.Set<TicketEscalation>()
                    .Where(e => e.TicketId == x.Ticket.Id)
                    .OrderByDescending(e => e.EscalatedAtUtc)
                    .ThenByDescending(e => e.Id)
                    .Select(e => (DateTime?)e.EscalatedAtUtc)
                    .FirstOrDefault() ?? x.Ticket.CreatedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
            return null;

        row.Item.SlaStatus = SlaEvaluator.Evaluate(
            row.Item.DueAtUtc,
            row.Item.ResolvedAtUtc,
            now,
            row.WindowStartUtc,
            AtRiskPercent);

        return row.Item;
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(
        int customerId,
        CancellationToken ct)
    {
        var now = clock.UtcNow;
        var rows = await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new
            {
                Item = new TicketListItemDto
                {
                    Id = x.Ticket.Id,
                    Reference = x.Ticket.Reference,
                    Title = x.Ticket.Title,
                    Status = x.Ticket.Status,
                    Priority = x.Ticket.Priority,
                    Customer = new CustomerSummaryDto(
                        x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                    Category = new CategorySummaryDto(
                        x.Category.Id, x.Category.Name),
                    AssignedAgent = x.Agent == null
                        ? null
                        : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                    CreatedAtUtc = x.Ticket.CreatedAtUtc,
                    UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                    DueAtUtc = x.Ticket.DueAtUtc,
                    ResolvedAtUtc = x.Ticket.ResolvedAtUtc
                },
                WindowStartUtc = db.Set<TicketEscalation>()
                    .Where(e => e.TicketId == x.Ticket.Id)
                    .OrderByDescending(e => e.EscalatedAtUtc)
                    .ThenByDescending(e => e.Id)
                    .Select(e => (DateTime?)e.EscalatedAtUtc)
                    .FirstOrDefault() ?? x.Ticket.CreatedAtUtc
            })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            row.Item.SlaStatus = SlaEvaluator.Evaluate(
                row.Item.DueAtUtc,
                row.Item.ResolvedAtUtc,
                now,
                row.WindowStartUtc,
                AtRiskPercent);
        }

        return rows.Select(row => row.Item).ToList();
    }

    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>().AsNoTracking()
            on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>().AsNoTracking()
            on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>().AsNoTracking()
            .Where(a => a.Id == ticket.AssignedAgentId)
            .DefaultIfEmpty()
        select new TicketWithLabels
        {
            Ticket = ticket,
            Customer = customer,
            Category = category,
            Agent = agent
        };

    private IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source,
        TicketQuery query,
        DateTime now)
    {
        if (query.Status.HasValue)
            source = source.Where(x => x.Ticket.Status == query.Status.Value);

        if (query.Priority.HasValue)
            source = source.Where(x => x.Ticket.Priority == query.Priority.Value);

        if (query.CategoryId.HasValue)
            source = source.Where(x => x.Ticket.CategoryId == query.CategoryId.Value);

        if (query.CustomerId.HasValue)
            source = source.Where(x => x.Ticket.CustomerId == query.CustomerId.Value);

        if (query.AssignedAgentId.HasValue)
            source = source.Where(x => x.Ticket.AssignedAgentId == query.AssignedAgentId.Value);

        if (query.UnassignedOnly)
            source = source.Where(x => x.Ticket.AssignedAgentId == null);

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            source = source.Where(x =>
                x.Ticket.Title.Contains(search) ||
                x.Ticket.Reference.Contains(search) ||
                x.Customer.Name.Contains(search));
        }

        if (query.SlaStatus is { } requestedStatus)
        {
            var threshold = AtRiskPercent;
            var withWindow = source.Select(x => new
            {
                x.Ticket,
                x.Customer,
                x.Category,
                x.Agent,
                WindowStartUtc = db.Set<TicketEscalation>()
                    .Where(e => e.TicketId == x.Ticket.Id)
                    .OrderByDescending(e => e.EscalatedAtUtc)
                    .ThenByDescending(e => e.Id)
                    .Select(e => (DateTime?)e.EscalatedAtUtc)
                    .FirstOrDefault() ?? x.Ticket.CreatedAtUtc
            });

            var filtered = requestedStatus switch
            {
                SlaStatus.NotApplicable => withWindow.Where(x =>
                    x.Ticket.DueAtUtc == null),

                SlaStatus.Met => withWindow.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    x.Ticket.ResolvedAtUtc != null &&
                    x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc),

                SlaStatus.Breached => withWindow.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    ((x.Ticket.ResolvedAtUtc != null &&
                      x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                     (x.Ticket.ResolvedAtUtc == null && now > x.Ticket.DueAtUtc))),

                SlaStatus.AtRisk => withWindow.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    x.Ticket.ResolvedAtUtc == null &&
                    now <= x.Ticket.DueAtUtc &&
                    EF.Functions.DateDiffMillisecond(
                        x.WindowStartUtc, x.Ticket.DueAtUtc.Value) > 0 &&
                    EF.Functions.DateDiffMillisecond(
                        now, x.Ticket.DueAtUtc.Value) <=
                    EF.Functions.DateDiffMillisecond(
                        x.WindowStartUtc, x.Ticket.DueAtUtc.Value) * threshold / 100.0),

                SlaStatus.WithinSla => withWindow.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    x.Ticket.ResolvedAtUtc == null &&
                    now <= x.Ticket.DueAtUtc &&
                    (EF.Functions.DateDiffMillisecond(
                         x.WindowStartUtc, x.Ticket.DueAtUtc.Value) <= 0 ||
                     EF.Functions.DateDiffMillisecond(
                         now, x.Ticket.DueAtUtc.Value) >
                     EF.Functions.DateDiffMillisecond(
                         x.WindowStartUtc, x.Ticket.DueAtUtc.Value) * threshold / 100.0)),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(query.SlaStatus), "Unsupported SLA status.")
            };

            source = filtered.Select(x => new TicketWithLabels
            {
                Ticket = x.Ticket,
                Customer = x.Customer,
                Category = x.Category,
                Agent = x.Agent
            });
        }

        return source;
    }

    private static IQueryable<TicketWithLabels> ApplySort(
        IQueryable<TicketWithLabels> source,
        TicketQuery query)
    {
        var descending = !string.Equals(
            query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

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
