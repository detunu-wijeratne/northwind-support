
import type { LoginResponse } from '../../types/api';

const SESSION_KEY = 'northwind-support-session';

export function getSession(): LoginResponse | null {
  try {
    const stored = sessionStorage.getItem(SESSION_KEY);
    if (!stored) return null;

    const session = JSON.parse(stored) as LoginResponse;

    if (
      !session.token ||
      !session.agent ||
      !session.expiresAtUtc ||
      new Date(session.expiresAtUtc).getTime() <= Date.now()
    ) {
      clearSession();
      return null;
    }

    return session;
  } catch {
    clearSession();
    return null;
  }
}

export function saveSession(session: LoginResponse): void {
  sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
  window.dispatchEvent(new Event('northwind-auth-changed'));
}

export function clearSession(): void {
  sessionStorage.removeItem(SESSION_KEY);
  window.dispatchEvent(new Event('northwind-auth-changed'));
}
