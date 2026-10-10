import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type {
  PagedResult,
  TicketFilters,
  TicketListItem,
} from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);

  const [requestFilters, setRequestFilters] =
    useState<TicketFilters>(emptyFilters);

  const [state, setState] = useState<State>({
    isLoading: true,
  });

  // Latest user-selected filters.
  // Accessed only from event handlers.
  const filtersRef = useRef<TicketFilters>(emptyFilters);

  // Prevent older responses from replacing newer results.
  const requestId = useRef(0);

  // Only search-only changes are debounced.
  useEffect(() => {
    if (filters.search === requestFilters.search) {
      return;
    }

    const timer = window.setTimeout(() => {
      setRequestFilters((current) => ({
        ...current,
        search: filters.search,
      }));
    }, DEBOUNCE_MS);

    return () => window.clearTimeout(timer);
  }, [filters.search, requestFilters.search]);

  // Request tickets whenever effective filters change.
  useEffect(() => {
    const currentRequest = ++requestId.current;
    let cancelled = false;

    const loadTickets = async () => {
      setState((current) => ({
        ...current,
        isLoading: true,
        error: undefined,
      }));

      try {
        const data = await ticketsApi.getTickets(requestFilters);

        if (!cancelled && currentRequest === requestId.current) {
          setState({
            isLoading: false,
            data,
            error: undefined,
          });
        }
      } catch (error) {
        if (!cancelled && currentRequest === requestId.current) {
          setState((current) => ({
            ...current,
            isLoading: false,
            error:
              error instanceof ApiError
                ? error.message
                : 'Unable to load tickets.',
          }));
        }
      }
    };

    void loadTickets();

    return () => {
      cancelled = true;
    };
  }, [requestFilters]);

  const updateFilters = (patch: Partial<TicketFilters>) => {
    const current = filtersRef.current;

    const next: TicketFilters = {
      ...current,
      ...patch,
      page: patch.page ?? 1,
    };

    filtersRef.current = next;
    setFilters(next);

    const isSearchOnly =
      Object.keys(patch).length === 1 &&
      Object.prototype.hasOwnProperty.call(patch, 'search');

    // Dropdowns, pagination and sorting update immediately.
    // They also use the latest search text.
    if (!isSearchOnly) {
      setRequestFilters(next);
    }
  };

  const resetFilters = () => {
    filtersRef.current = emptyFilters;
    setFilters(emptyFilters);
    setRequestFilters(emptyFilters);
  };

  const reload = async () => {
    const currentRequest = ++requestId.current;

    setState((current) => ({
      ...current,
      isLoading: true,
      error: undefined,
    }));

    try {
      const data = await ticketsApi.getTickets(requestFilters);

      if (currentRequest === requestId.current) {
        setState({
          isLoading: false,
          data,
          error: undefined,
        });
      }
    } catch (error) {
      if (currentRequest === requestId.current) {
        setState((current) => ({
          ...current,
          isLoading: false,
          error:
            error instanceof ApiError
              ? error.message
              : 'Unable to load tickets.',
        }));
      }
    }
  };

  return {
    filters,
    updateFilters,
    resetFilters,
    reload,
    ...state,
  };
}
