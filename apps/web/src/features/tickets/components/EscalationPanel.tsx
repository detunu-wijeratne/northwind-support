
import { useState, type FormEvent } from 'react';
import { request, toErrorMessage } from '../../../api/client';
import type {
  EscalateTicketPayload,
  EscalateTicketResponse,
  TicketDetail,
} from '../../../types/api';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (ticket: TicketDetail) => Promise<void>;
}

export function EscalationPanel({
  ticket,
  onEscalated,
}: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const cannotEscalate =
    ticket.priority === 'Critical' ||
    ticket.status === 'Resolved' ||
    ticket.status === 'Closed';

  const trimmedReason = reason.trim();

  const reasonValid =
    trimmedReason.length >= 5 &&
    trimmedReason.length <= 500;

  const canSubmit =
    !cannotEscalate &&
    !pending &&
    reasonValid;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!canSubmit) {
      return;
    }

    setError(null);
    setPending(true);

    try {
      const payload: EscalateTicketPayload = {
        reason: trimmedReason,
      };

      const response = await request<EscalateTicketResponse>(
        `/tickets/${ticket.id}/escalate`,
        {
          method: 'POST',
          body: JSON.stringify(payload),
        },
      );

      setReason('');
      await onEscalated(response.ticket);
    } catch (caught) {
      setError(
        toErrorMessage(caught, 'Unable to escalate this ticket.'),
      );
    } finally {
      setPending(false);
    }
  }

  return (
    <section
      className="card"
      aria-labelledby="escalation-heading"
    >
      <h2 id="escalation-heading">Escalate ticket</h2>

      <p>
        Increase this ticket&apos;s priority by one level and
        recalculate its SLA deadline and assignment.
      </p>

      {cannotEscalate ? (
        <p role="status">
          This ticket cannot be escalated because it is
          {ticket.priority === 'Critical'
            ? ' already Critical.'
            : ` ${ticket.status}.`}
        </p>
      ) : (
        <form onSubmit={(event) => void handleSubmit(event)}>
          <div style={{ marginBottom: '1rem' }}>
            <label htmlFor="escalation-reason">
              Reason for escalation
            </label>

            <textarea
              id="escalation-reason"
              value={reason}
              onChange={(event) => {
                setReason(event.target.value);
                setError(null);
              }}
              rows={4}
              maxLength={500}
              required
              aria-describedby="escalation-reason-help"
              style={{
                display: 'block',
                width: '100%',
                marginTop: '0.5rem',
              }}
            />

            <p id="escalation-reason-help">
              5–500 characters required.{' '}
              {trimmedReason.length}/500
            </p>
          </div>

          {error && (
            <p role="alert" style={{ color: '#b42318' }}>
              {error}
            </p>
          )}

          <button
            type="submit"
            disabled={!canSubmit}
          >
            {pending ? 'Escalating...' : 'Escalate ticket'}
          </button>
        </form>
      )}
    </section>
  );
}
