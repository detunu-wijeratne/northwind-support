
import {
  beforeEach,
  afterEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest';
import { request } from './client';
import { getSession, saveSession } from '../features/auth/session';
import type { LoginResponse } from '../types/api';

const session: LoginResponse = {
  token: 'test-jwt-token',
  expiresAtUtc: '2099-01-01T00:00:00Z',
  agent: {
    id: 1,
    fullName: 'Alex Turner',
    email: 'alex.turner@northwind-support.example',
  },
};

describe('Authenticated API client', () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    sessionStorage.clear();
  });

  it('attaches the JWT to protected requests', async () => {
    saveSession(session);

    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(JSON.stringify({ items: [] }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    await request('/tickets');

    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining('/tickets'),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-jwt-token',
        }),
      }),
    );
  });

  it('clears the stored session after HTTP 401', async () => {
    saveSession(session);

    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(
        JSON.stringify({ title: 'Unauthorized' }),
        {
          status: 401,
          headers: { 'Content-Type': 'application/json' },
        },
      ),
    );

    await expect(request('/tickets')).rejects.toMatchObject({
      status: 401,
    });

    expect(getSession()).toBeNull();
  });

  it('does not attach a token to the login request when signed out', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(JSON.stringify(session), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    await request('/auth/login', {
      method: 'POST',
      body: JSON.stringify({
        email: 'alex.turner@northwind-support.example',
        password: 'example',
      }),
    });

    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining('/auth/login'),
      expect.objectContaining({
        headers: expect.not.objectContaining({
          Authorization: expect.any(String),
        }),
      }),
    );
  });
});
