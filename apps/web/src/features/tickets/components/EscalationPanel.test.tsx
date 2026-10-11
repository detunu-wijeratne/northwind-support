
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, request } from '../../../api/client';
import type { TicketDetail } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../../api/client')>();

  return {
    ...original,
    request: vi.fn(),
  };
});

const mockedRequest = vi.mocked(request);

const ticket: TicketDetail = {
  id: 41,
  reference: 'TCK-0041',
  title: 'Website login issue after password reset',
  description: 'Customer cannot log in.',
  status: 'New',
  priority: 'High',
  customer: {
    id: 2,
    name: 'Fabrikam Inc.',
    tier: 'Standard',
    email: 'support@fabrikam.example',
    phone: null,
  },
  category: {
    id: 1,
    name: 'General',
    requiresSpecialist: false,
  },
  assignedAgent: {
    id: 2,
    fullName: 'Ben Osei',
  },
  createdAtUtc: '2026-10-11T12:26:00Z',
  updatedAtUtc: '2026-10-11T13:05:00Z',
  dueAtUtc: '2026-10-11T21:05:00Z',
  resolvedAtUtc: null,
  slaStatus: 'WithinSla',
};

describe('EscalationPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('does not call the API when the reason is shorter than 5 characters', () => {
    const onEscalated = vi.fn().mockResolvedValue(undefined);

    render(
      <EscalationPanel ticket={ticket} onEscalated={onEscalated} />,
    );

    fireEvent.change(screen.getByLabelText('Reason for escalation'), {
      target: { value: 'abc' },
    });

    fireEvent.change(screen.getByLabelText('Escalated by'), {
      target: { value: 'Test Agent' },
    });

    const button = screen.getByRole('button', {
      name: 'Escalate ticket',
    });

    expect(button).toBeDisabled();

    fireEvent.click(button);

    expect(mockedRequest).not.toHaveBeenCalled();
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('shows an HTTP 409 error without updating the ticket', async () => {
    const onEscalated = vi.fn().mockResolvedValue(undefined);

    mockedRequest.mockRejectedValueOnce(
      new ApiError(
        409,
        'This ticket cannot be escalated.',
      ),
    );

    render(
      <EscalationPanel ticket={ticket} onEscalated={onEscalated} />,
    );

    fireEvent.change(screen.getByLabelText('Reason for escalation'), {
      target: {
        value: 'Customer operations are significantly affected.',
      },
    });

    fireEvent.change(screen.getByLabelText('Escalated by'), {
      target: { value: 'Test Agent' },
    });

    fireEvent.click(
      screen.getByRole('button', { name: 'Escalate ticket' }),
    );

    await waitFor(() => {
      expect(
        screen.getByRole('alert'),
      ).toHaveTextContent('This ticket cannot be escalated.');
    });

    expect(mockedRequest).toHaveBeenCalledWith(
      '/tickets/41/escalate',
      expect.objectContaining({
        method: 'POST',
      }),
    );

    expect(onEscalated).not.toHaveBeenCalled();

    expect(
      screen.getByLabelText('Reason for escalation'),
    ).toHaveValue(
      'Customer operations are significantly affected.',
    );
  });

  it('disables escalation for Critical tickets', () => {
    const onEscalated = vi.fn().mockResolvedValue(undefined);

    render(
      <EscalationPanel
        ticket={{ ...ticket, priority: 'Critical' }}
        onEscalated={onEscalated}
      />,
    );

    expect(
      screen.getByText(/already Critical/i),
    ).toBeInTheDocument();

    expect(
      screen.queryByRole('button', { name: 'Escalate ticket' }),
    ).not.toBeInTheDocument();

    expect(mockedRequest).not.toHaveBeenCalled();
  });

  it('disables escalation for Resolved tickets', () => {
    const onEscalated = vi.fn().mockResolvedValue(undefined);

    render(
      <EscalationPanel
        ticket={{ ...ticket, status: 'Resolved' }}
        onEscalated={onEscalated}
      />,
    );

    expect(
      screen.getByText(/Resolved/i),
    ).toBeInTheDocument();

    expect(
      screen.queryByRole('button', { name: 'Escalate ticket' }),
    ).not.toBeInTheDocument();
  });
});
