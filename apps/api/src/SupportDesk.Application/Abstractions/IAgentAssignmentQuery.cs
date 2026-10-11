
namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Finds eligible agents for automatic ticket assignment.
/// </summary>
public interface IAgentAssignmentQuery
{
    /// <summary>
    /// Finds the eligible agent with the fewest open tickets.
    /// </summary>
    Task<int?> FindBestAgentIdAsync(
        int categoryId,
        bool requiresSpecialist,
        CancellationToken ct);

    /// <summary>
    /// Checks whether an existing agent remains eligible
    /// to keep a ticket during escalation.
    /// </summary>
    Task<bool> CanKeepAssignmentAsync(
        int agentId,
        int categoryId,
        bool requiresSpecialist,
        CancellationToken ct);
}
