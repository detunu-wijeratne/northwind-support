
import type { Escalation } from '../../../types/api';

interface EscalationHistoryProps {
  history: Escalation[];
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}

function formatDate(date: string | null): string {
  if (!date) return 'Not set';

  return new Date(date).toLocaleString();
}

function formatAgent(agentId: number | null): string {
  return agentId === null
    ? 'Unassigned'
    : `Agent #${agentId}`;
}

export function EscalationHistory({
  history,
  isLoading,
  error,
  onRetry,
}: EscalationHistoryProps) {
  return (
    <section className="card" aria-labelledby="escalation-history-heading">
      <h2 id="escalation-history-heading">Escalation history</h2>

      {isLoading && <p role="status">Loading history...</p>}

      {error && (
        <div role="alert">
          <p>{error}</p>
          <button type="button" onClick={onRetry}>
            Retry
          </button>
        </div>
      )}

      {!isLoading && !error && history.length === 0 && (
        <p>No escalations have been recorded for this ticket.</p>
      )}

      {!isLoading && !error && history.length > 0 && (
        <ol style={{ paddingLeft: '1.25rem' }}>
          {history.map((entry) => (
            <li key={entry.id} style={{ marginBottom: '1.5rem' }}>
              <h3>
                {entry.fromPriority} → {entry.toPriority}
              </h3>

              <p>
                <strong>Reason:</strong> {entry.reason}
              </p>

              <p>
                <strong>Escalated by:</strong> {entry.escalatedBy}
              </p>

              <p>
                <strong>When:</strong>{' '}
                {formatDate(entry.escalatedAtUtc)}
              </p>

              <p>
                <strong>Agent:</strong>{' '}
                {formatAgent(entry.fromAgentId)} →{' '}
                {formatAgent(entry.toAgentId)}
              </p>

              <p>
                <strong>SLA deadline:</strong>{' '}
                {formatDate(entry.fromDueAtUtc)} →{' '}
                {formatDate(entry.toDueAtUtc)}
              </p>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
