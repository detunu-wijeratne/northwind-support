
import {
  Link,
  NavLink,
  Outlet,
  useNavigate,
} from 'react-router-dom';
import { useAuth } from './features/auth/useAuth';

/** Authenticated application shell. */
export function App() {
  const { agent, logout } = useAuth();
  const navigate = useNavigate();

  function handleLogout() {
    logout();
    void navigate('/login', { replace: true });
  }

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>

        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '0.75rem',
          }}
        >
          <span>{agent?.fullName}</span>

          <button
            type="button"
            className="button button--ghost"
            onClick={handleLogout}
          >
            Logout
          </button>
        </div>
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
