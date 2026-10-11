using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));

    
    [Fact]
    public void Evaluate_AtRisk_WhenExactly25PercentRemains()
    {
        var start = new DateTime(
            2026, 10, 11, 10, 0, 0, DateTimeKind.Utc);

        var due = start.AddHours(8);
        var now = start.AddHours(6);

        var result = SlaEvaluator.Evaluate(
            due,
            null,
            now,
            start,
            25);

        Assert.Equal(SlaStatus.AtRisk, result);
    }

    [Fact]
    public void Evaluate_WithinSla_JustBeforeAtRiskThreshold()
    {
        var start = new DateTime(
            2026, 10, 11, 10, 0, 0, DateTimeKind.Utc);

        var due = start.AddHours(8);
        var now = start.AddHours(6).AddTicks(-1);

        var result = SlaEvaluator.Evaluate(
            due,
            null,
            now,
            start,
            25);

        Assert.Equal(SlaStatus.WithinSla, result);
    }

    [Fact]
    public void Evaluate_AtRisk_AtExactDeadline()
    {
        var start = new DateTime(
            2026, 10, 11, 10, 0, 0, DateTimeKind.Utc);

        var due = start.AddHours(8);

        var result = SlaEvaluator.Evaluate(
            due,
            null,
            due,
            start,
            25);

        Assert.Equal(SlaStatus.AtRisk, result);
    }

    [Fact]
    public void Evaluate_Breached_ImmediatelyAfterDeadline()
    {
        var start = new DateTime(
            2026, 10, 11, 10, 0, 0, DateTimeKind.Utc);

        var due = start.AddHours(8);

        var result = SlaEvaluator.Evaluate(
            due,
            null,
            due.AddTicks(1),
            start,
            25);

        Assert.Equal(SlaStatus.Breached, result);
    }

}
