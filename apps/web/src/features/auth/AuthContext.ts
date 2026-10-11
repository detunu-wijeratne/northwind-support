import { createContext } from 'react';
import type { AuthAgent, LoginPayload } from '../../types/api';

export interface AuthContextValue {
  agent: AuthAgent | null;
  isAuthenticated: boolean;
  login: (payload: LoginPayload) => Promise<void>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);