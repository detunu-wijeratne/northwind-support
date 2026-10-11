
import {
  useEffect,
  useState,
  type ReactNode,
} from 'react';
import type {
  LoginPayload,
  LoginResponse,
} from '../../types/api';
import { authApi } from '../../api/auth';
import { AuthContext } from './AuthContext';
import {
  clearSession,
  getSession,
  saveSession,
} from './session';

export function AuthProvider({
  children,
}: {
  children: ReactNode;
}) {
  const [session, setSession] = useState<LoginResponse | null>(
    () => getSession(),
  );

  useEffect(() => {
    const syncSession = () => {
      setSession(getSession());
    };

    window.addEventListener('northwind-auth-changed', syncSession);
    window.addEventListener('storage', syncSession);

    return () => {
      window.removeEventListener('northwind-auth-changed', syncSession);
      window.removeEventListener('storage', syncSession);
    };
  }, []);

  async function login(payload: LoginPayload): Promise<void> {
    const result = await authApi.login(payload);

    saveSession(result);
    setSession(result);
  }

  function logout(): void {
    clearSession();
    setSession(null);
  }

  return (
    <AuthContext.Provider
      value={{
        agent: session?.agent ?? null,
        isAuthenticated: session !== null,
        login,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
