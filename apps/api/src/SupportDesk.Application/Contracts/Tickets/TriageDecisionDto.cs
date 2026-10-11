namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Explains the decisions made during automatic ticket triage.
/// </summary>
public sealed class TriageDecisionDto
{
    public string PriorityReason { get; set; } = string.Empty;

    public string SlaReason { get; set; } = string.Empty;

    public string AssignmentReason { get; set; } = string.Empty;
}