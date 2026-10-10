
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests
{
    private static readonly DateTime TestTime =
        new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    // Creates a fresh relational database for each test.
    private static async Task<SqliteTestContext> CreateContextAsync()
    {
        var connection = new SqliteConnection(
            "Data Source=:memory:");

        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new SupportDbContext(options);

        await db.Database.EnsureCreatedAsync();

        // Create customers.
        var contoso = new Customer(
            "Contoso Ltd",
            "support@contoso.test",
            null,
            CustomerTier.Premium,
            TestTime);

        var fabrikam = new Customer(
            "Fabrikam Ltd",
            "support@fabrikam.test",
            null,
            CustomerTier.Standard,
            TestTime);

        // Create category.
        var general = new Category(
            "General",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.Set<Customer>().AddRange(contoso, fabrikam);
        db.Set<Category>().Add(general);

        await db.SaveChangesAsync();

        // Create tickets using the domain factory.
        var ticket1 = Ticket.Raise(
            "TCK-0001",
            "Login problem",
            "Unable to log in",
            contoso.Id,
            general.Id,
            TicketPriority.High,
            TestTime);

        var ticket2 = Ticket.Raise(
            "TCK-0002",
            "Payment issue",
            "Incorrect payment",
            contoso.Id,
            general.Id,
            TicketPriority.Medium,
            TestTime);

        var ticket3 = Ticket.Raise(
            "TCK-0003",
            "Website error",
            "Website unavailable",
            fabrikam.Id,
            general.Id,
            TicketPriority.High,
            TestTime);

        var ticket4 = Ticket.Raise(
            "TCK-0004",
            "Account question",
            "Customer account query",
            fabrikam.Id,
            general.Id,
            TicketPriority.Low,
            TestTime);

        // Mark one High-priority ticket as InProgress.
        ticket3.ChangeStatus(
            TicketStatus.InProgress,
            TestTime);

        db.Set<Ticket>().AddRange(
            ticket1,
            ticket2,
            ticket3,
            ticket4);

        await db.SaveChangesAsync();

        // Use a fixed clock to make test results predictable.
        var clock = new Mock<IClock>();

        clock.Setup(c => c.UtcNow)
            .Returns(TestTime);

        var queries = new TicketQueries(
            db,
            clock.Object);

        return new SqliteTestContext(
            connection,
            db,
            queries);
    }

    // TEST 1: Exact-match priority filtering.
    [Fact]
    public async Task GetPagedAsync_FiltersByExactPriority()
    {
        using var context = await CreateContextAsync();

        var query = new TicketQuery
        {
            Priority = TicketPriority.High
        };

        var result = await context.Queries.GetPagedAsync(
            query,
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        Assert.All(result.Items, ticket =>
            Assert.Equal(TicketPriority.High, ticket.Priority));
    }

    // TEST 2: Multiple filters must combine using AND.
    [Fact]
    public async Task GetPagedAsync_CombinesStatusAndPriority()
    {
        using var context = await CreateContextAsync();

        var query = new TicketQuery
        {
            Priority = TicketPriority.High,
            Status = TicketStatus.InProgress
        };

        var result = await context.Queries.GetPagedAsync(
            query,
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);

        var ticket = result.Items.Single();

        Assert.Equal("TCK-0003", ticket.Reference);
        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
    }

    // TEST 3: Search matches customer name.
    [Fact]
    public async Task GetPagedAsync_SearchesCustomerName()
    {
        using var context = await CreateContextAsync();

        var query = new TicketQuery
        {
            Search = "  Contoso  "
        };

        var result = await context.Queries.GetPagedAsync(
            query,
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        Assert.All(result.Items, ticket =>
            Assert.Equal("Contoso Ltd", ticket.Customer.Name));
    }

    // TEST 4: Filtering happens before pagination.
    [Fact]
    public async Task GetPagedAsync_CountsFilteredResultsBeforePaging()
    {
        using var context = await CreateContextAsync();

        var query = new TicketQuery
        {
            Priority = TicketPriority.High,
            Page = 1,
            PageSize = 1
        };

        var result = await context.Queries.GetPagedAsync(
            query,
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    // TEST 5: Unassigned-only filtering.
    [Fact]
    public async Task GetPagedAsync_ReturnsOnlyUnassignedTickets()
    {
        using var context = await CreateContextAsync();

        var query = new TicketQuery
        {
            UnassignedOnly = true
        };

        var result = await context.Queries.GetPagedAsync(
            query,
            CancellationToken.None);

        Assert.Equal(4, result.TotalCount);

        Assert.All(result.Items, ticket =>
            Assert.Null(ticket.AssignedAgent));
    }

    // Releases both the EF context and SQLite connection.
    private sealed class SqliteTestContext(
        SqliteConnection connection,
        SupportDbContext db,
        TicketQueries queries) : IDisposable
    {
        public TicketQueries Queries { get; } = queries;

        public void Dispose()
        {
            db.Dispose();
            connection.Dispose();
        }
    }
}
