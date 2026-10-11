
import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { request, toErrorMessage } from '../../api/client';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useReferenceData } from '../../hooks/useReferenceData';
import type { Escalation, TicketDetail } from '../../types/api';
import { AssignAgentControl } from './components/AssignAgentControl';
import { EscalationHistory } from './components/EscalationHistory';
import { EscalationPanel } from './components/EscalationPanel';
import { StatusActions } from './components/StatusActions';
import { TicketSummary } from './components/TicketSummary';
import { useTicket } from './hooks/useTicket';

export function TicketDetailPage() {
  const { id } = useParams<{ id: string }>();
  const ticketId = Number(id);

  const { data: ticket, isLoading, error, reload, setData } =
    useTicket(ticketId);

  const reference = useReferenceData();

  const [history, setHistory] = useState<Escalation[]>([]);
  const [historyLoading, setHistoryLoading] = useState(true);
  const [historyError, setHistoryError] = useState<string | null>(
    null,
  );

  const loadHistory = useCallback(async () => {
    if (!Number.isInteger(ticketId) || ticketId <= 0) {
      setHistoryLoading(false);
      return;
    }

    setHistoryLoading(true);
    setHistoryError(null);

    try {
      const result = await request<Escalation[]>(
        `/tickets/${ticketId}/escalations`,
      );

      setHistory(result);
    } catch (caught) {
      setHistoryError(
        toErrorMessage(caught, 'Unable to load escalation history.'),
      );
    } finally {
      setHistoryLoading(false);
    }
  }, [ticketId]);

  useEffect(() => {
  const controller = new AbortController();

  async function fetchHistory() {
    try {
      const result = await request<Escalation[]>(
        `/tickets/${ticketId}/escalations`,
        { signal: controller.signal },
      );

      if (!controller.signal.aborted) {
        setHistory(result);
        setHistoryError(null);
        setHistoryLoading(false);
      }
    } catch (caught) {
      if (!controller.signal.aborted) {
        setHistoryError(
          toErrorMessage(caught, 'Unable to load escalation history.'),
        );
        setHistoryLoading(false);
      }
    }
  }

  if (Number.isInteger(ticketId) && ticketId > 0) {
    void fetchHistory();
  }

  return () => controller.abort();
}, [ticketId]);

  async function handleEscalated(updatedTicket: TicketDetail) {
    setData(updatedTicket);

    // Refresh both the ticket and its escalation history
    // without reloading the browser.
    await Promise.all([
      reload(),
      loadHistory(),
    ]);
  }

  if (!Number.isInteger(ticketId) || ticketId <= 0) {
    return (
      <ErrorMessage
        message="That is not a valid ticket reference."
      />
    );
  }

  if (isLoading && !ticket) {
    return <Spinner label="Loading ticket" />;
  }

  if (error && !ticket) {
    return (
      <ErrorMessage
        message={error}
        onRetry={() => void reload()}
      />
    );
  }

  if (!ticket) {
    return null;
  }

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/tickets">Tickets</Link>
            {' / '}
            {ticket.reference}
          </p>

          <h1>{ticket.title}</h1>
        </div>
      </header>

      {error && (
        <ErrorMessage
          message={error}
          onRetry={() => void reload()}
        />
      )}

      <div className="detail-layout">
        <div className="detail-layout__main">
          <div className="card">
            <h2>Description</h2>
            <p className="description">{ticket.description}</p>
          </div>

          <StatusActions
            ticket={ticket}
            onUpdated={setData}
          />

          <EscalationPanel
            ticket={ticket}
            onEscalated={handleEscalated}
          />

          <EscalationHistory
            history={history}
            isLoading={historyLoading}
            error={historyError}
            onRetry={() => void loadHistory()}
          />
        </div>

        <aside className="detail-layout__side">
          <TicketSummary ticket={ticket} />

          <AssignAgentControl
            ticket={ticket}
            agents={reference.data?.agents ?? []}
            onUpdated={setData}
          />
        </aside>
      </div>
    </section>
  );
}
