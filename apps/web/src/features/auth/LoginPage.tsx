
import { useState, type FormEvent } from 'react';
import {
  Navigate,
  useLocation,
  useNavigate,
} from 'react-router-dom';
import { useAuth } from './useAuth';

export function LoginPage() {
  const { login, isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const state = location.state as { from?: string } | null;
  const requestedPath = state?.from;
  const destination =
    requestedPath?.startsWith('/') &&
    !requestedPath.startsWith('//') &&
    !requestedPath.startsWith('/\\')
      ? requestedPath
      : '/tickets';

  if (isAuthenticated) {
    return <Navigate to={destination} replace />;
  }

  const emailValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(
    email.trim(),
  );

  const canSubmit =
    emailValid &&
    password.length > 0 &&
    !pending;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!canSubmit) return;

    setPending(true);
    setError(null);

    try {
      await login({
        email: email.trim(),
        password,
      });

      void navigate(destination, { replace: true });
    } catch {
      // Use the same message for wrong email and wrong password.
      setError('Invalid email or password.');
    } finally {
      setPending(false);
    }
  }

  return (
    <main className="app__main">
      <section
        className="card"
        style={{
          maxWidth: '440px',
          margin: '4rem auto',
          padding: '2rem',
        }}
      >
        <h1>Agent sign in</h1>

        <p>Sign in to access Northwind Support Desk.</p>

        <form onSubmit={(event) => void handleSubmit(event)}>
          <div className="field" style={{ marginBottom: '1rem' }}>
            <label className="field__label" htmlFor="login-email">
              Email address
            </label>

            <input
              id="login-email"
              type="email"
              autoComplete="username"
              value={email}
              onChange={(event) => {
                setEmail(event.target.value);
                setError(null);
              }}
              required
            />
          </div>

          <div className="field" style={{ marginBottom: '1rem' }}>
            <label
              className="field__label"
              htmlFor="login-password"
            >
              Password
            </label>

            <input
              id="login-password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => {
                setPassword(event.target.value);
                setError(null);
              }}
              required
            />
          </div>

          {error && (
            <p role="alert" style={{ color: '#b42318' }}>
              {error}
            </p>
          )}

          <button
            type="submit"
            className="button button--primary"
            disabled={!canSubmit}
          >
            {pending ? 'Signing in...' : 'Sign in'}
          </button>
        </form>
      </section>
    </main>
  );
}
