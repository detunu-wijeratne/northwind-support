
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useReferenceData } from '../../hooks/useReferenceData';
import { formatDateTime } from '../../lib/format';
import type { TicketDetail } from '../../types/api';
import { CreateTicketForm } from './components/CreateTicketForm';
import { PriorityBadge } from './components/TicketBadges';

export function CreateTicketPage() {
  const reference = useReferenceData();
  const navigate = useNavigate();
  const [created, setCreated] = useState<TicketDetail>();

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/tickets">Tickets</Link> / New
          </p>
          <h1>Raise a ticket</h1>
        </div>
      </header>

      {reference.isLoading && (
        <Spinner label="Loading customers and categories" />
      )}

      {reference.error && (
        <ErrorMessage
          message={reference.error}
          onRetry={() => void reference.reload()}
        />
      )}

      {created && (
        <div className="card notice" role="status">
          <h2>Ticket created successfully</h2>

          <p>
            Raised <strong>{created.reference}</strong> for{' '}
            {created.customer.name}.
          </p>

          <h3>Automatic triage results</h3>

          <dl className="summary">
            <div>
              <dt>Calculated priority</dt>
              <dd>
                <PriorityBadge priority={created.priority} />
              </dd>
            </div>

            <div>
              <dt>SLA deadline</dt>
              <dd>{formatDateTime(created.dueAtUtc)}</dd>
            </div>

            <div>
              <dt>Assigned agent</dt>
              <dd>
                {created.assignedAgent?.fullName ?? 'Unassigned'}
              </dd>
            </div>

            <div>
              <dt>Customer tier</dt>
              <dd>{created.customer.tier}</dd>
            </div>
          </dl>

          {created.triage ? (
            <div className="triage-explanations">
              <h3>Why these decisions were made</h3>

              <p>
                <strong>Priority:</strong>{' '}
                {created.triage.priorityReason}
              </p>

              <p>
                <strong>SLA:</strong>{' '}
                {created.triage.slaReason}
              </p>

              <p>
                <strong>Assignment:</strong>{' '}
                {created.triage.assignmentReason}
              </p>
            </div>
          ) : (
            <p>
              Detailed triage explanations were not included
              in the server response.
            </p>
          )}

          <div className="button-row">
            <button
              type="button"
              className="button button--primary"
              onClick={() => void navigate(`/tickets/${created.id}`)}
            >
              Open {created.reference}
            </button>

            <button
              type="button"
              className="button button--ghost"
              onClick={() => setCreated(undefined)}
            >
              Raise another
            </button>
          </div>
        </div>
      )}

      {reference.data && !created && (
        <CreateTicketForm
          customers={reference.data.customers}
          categories={reference.data.categories}
          onCreated={setCreated}
        />
      )}
    </section>
  );
}
