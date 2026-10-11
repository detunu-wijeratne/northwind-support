
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaCell } from './SlaCell';

describe('SlaCell', () => {
  const cases: { status: SlaStatus; text: string }[] = [
    { status: 'NotApplicable', text: 'No SLA' },
    { status: 'WithinSla', text: 'Within SLA' },
    { status: 'AtRisk', text: 'At Risk' },
    { status: 'Breached', text: 'Breached' },
    { status: 'Met', text: 'Met' },
  ];

  it.each(cases)('displays $status as "$text"', ({ status, text }) => {
    render(
      <SlaCell
        dueAtUtc="2026-10-11T14:15:00Z"
        slaStatus={status}
      />,
    );

    expect(screen.getByText(text)).toBeInTheDocument();
  });
});
