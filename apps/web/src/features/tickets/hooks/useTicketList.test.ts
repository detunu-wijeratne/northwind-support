import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  // TEST 1: Search debounce
  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // No request during the debounce window
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({
        search: 'invoice',
        page: 1,
      }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });

  // TEST 2: DEFECT-117 - Status change
  it('sends a request immediately when status changes', async () => {
    const { result } = renderHook(() => useTicketList());

    // Complete initial request
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    // Change only status
    act(() => {
      result.current.updateFilters({ status: 'InProgress' });
    });

    await act(async () => {
      await Promise.resolve();
    });

    // Status change should trigger a new request
    expect(getTickets).toHaveBeenCalledTimes(2);

    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({
        status: 'InProgress',
        page: 1,
      }),
    );
  });

  // TEST 3: Status change while search is debouncing
  it('requests immediately when status changes during search debounce', async () => {
    const { result } = renderHook(() => useTicketList());

    // Complete initial request
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    // User starts typing in Search
    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Search should not make an immediate request
    expect(getTickets).toHaveBeenCalledTimes(1);

    // Change status before search debounce finishes
    act(() => {
      result.current.updateFilters({ status: 'InProgress' });
    });

    await act(async () => {
      await Promise.resolve();
    });

    // Status change should trigger an immediate request
    expect(getTickets).toHaveBeenCalledTimes(2);

    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({
        status: 'InProgress',
        search: 'invoice',
        page: 1,
      }),
    );
  });
});
