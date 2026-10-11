# Northwind Support Desk

A support ticket management system built with **.NET 10 Web API**, **React 19 / TypeScript**, **EF Core**, and **SQL Server**. This fork completes the candidate assignment's defect fix, filtering, ticket triage/escalation, agent authentication, and Docker API deployment.

## What was implemented

| Task | Implementation |
| --- | --- |
| 1 — Defect and filtering | Fixed stale ticket-list filters at the request trigger, added a regression test, and applied combined filters in SQL before counting and pagination. |
| 2 — Triage and escalation | Automatic category/priority rules, configurable SLA windows, deterministic eligible-agent assignment, escalation history, SLA indicator/filter, and ticket-detail/create UI. |
| 3 — Authentication | Salted password hashes for agents, JWT login, `/api/auth/me`, centrally protected API, JWT-derived escalation actor, React login/logout, and protected routes. |
| 4 — Docker | Multi-stage .NET 10 API image, non-root execution, SQL Server health dependency, runtime environment configuration, automatic development migrations/seeding, and public database health endpoint. |

## Setup and development login

**Prerequisites:** .NET 10 SDK, Node.js 20.19+ (Node 22 recommended), npm, and Docker Desktop.

**Development-only seeded account:**

- Email: `alex.turner@northwind-support.example`
- Password: `NorthwindDev2026!`

The password is a **local test fixture** hashed when demo agents are seeded; do not reuse it for a real deployment. An inactive seeded agent cannot sign in.

### Docker: recommended way to run the API

From the repository root, copy `.env.example` to `.env`. Set `MSSQL_SA_PASSWORD` and a **unique** `Jwt__Key` of at least 32 UTF-8 bytes in the local `.env`. Keep `.env` untracked. Generate a signing key in PowerShell:

```powershell
$bytes = New-Object byte[] 64
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()
[Convert]::ToBase64String($bytes)
```

Copy that output into `.env` as `Jwt__Key=<generated-value>` (without angle brackets). Run:

```powershell
docker compose up -d --build
docker compose ps
```

The API is exposed at `http://localhost:5080`, Swagger at `http://localhost:5080/swagger`, and the anonymous database check at `http://localhost:5080/health`. SQL Server is reached by the Compose hostname `sqlserver` rather than `localhost`. On Development startup, the API applies EF Core migrations and seeds demo data; existing data is preserved.

Start the React frontend separately (the web app is intentionally not Dockerized):

```powershell
npm install
$env:VITE_API_PROXY_TARGET = "http://localhost:5080"
npm run web
```

Open the Vite URL (normally `http://localhost:5173`) and sign in. The frontend sends the JWT on protected API calls. With no token, `/api/tickets` returns 401; `/health` remains public.

**Running the API directly:** `npm run db:up` starts SQL Server. Set `Jwt__Key` and `Jwt__Issuer=NorthwindSupport` in the API process environment before `npm run api`; ASP.NET Core `dotnet run` does **not** read `.env` automatically. Set `VITE_API_PROXY_TARGET` to the URL used by that API when starting the web app.

## API and business rules

- `POST /api/auth/login` is public and returns a JWT, expiry and agent details; `GET /api/auth/me` requires a JWT. Invalid email/password have the same 401 response; inactive agents cannot log in.
- `GET /api/tickets` supports exact `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, trimmed `search` (title/reference/customer), and `slaStatus`. Filters combine with **AND**, run in SQL **before** `totalCount`, sorting and paging, and do not filter the returned page in memory.
- `POST /api/tickets` automatically determines priority, SLA due date and an eligible agent, returning explanations. Security/outage-style categories use **category flags**, not hard-coded category names.
- SLA base windows are **Critical 4h, High 8h, Medium 24h, Low 72h**. Premium customers get half the window, with a minimum of 1h. Windows, multiplier and at-risk threshold come from configuration. SLA status is calculated, not persisted.
- Assignment considers active agents, required category specializations and maximum open-ticket limits; ties are deterministic. A ticket may be created unassigned with an explanation if no agent is eligible.
- `POST /api/tickets/{id}/escalate` increases priority one level, recalculates SLA and assignment, and saves immutable history. The request supplies only a 5–500 character reason; `escalatedBy` is derived from the authenticated JWT. Critical or resolved/closed tickets cannot be escalated. `GET /api/tickets/{id}/escalations` returns the history.
- `GET /health` reports database reachability, is anonymous, and returns 200 when healthy or 503 when unhealthy.

### Technical decisions and trade-offs

**Defect (DEFECT-117):** The ticket filters and the request-triggering state were out of sync: a status change did not initiate a new request until another input changed. The fix connects every filter change to the query request while retaining debouncing for search. The regression test asserts the request itself, rather than hiding the issue with in-memory filtering or a forced remount.

**Filtering:** Keep query composition in EF Core/IQueryable, adding one predicate per supplied filter before count/pagination. This allows a new filter such as `slaStatus` without restructuring the data path. Review the EF-generated SQL/logging to confirm predicates, count and paging remain database-side; avoid a large `ToList()` before filtering.

**Rules and patterns:** Shared triage/SLA/assignment services keep business logic out of controllers (**service pattern / single responsibility**). Repository and unit-of-work boundaries separate persistence and business behavior. An injectable clock permits deterministic time-boundary testing. We rejected hard-coded category-name checks and frontend-calculated assignment because both duplicate or weaken the source of truth.

**Authentication:** Credentials are stored as a `PasswordHash` on `Agent`, avoiding a separate users table where the agent already represents the sign-in identity. ASP.NET Core `PasswordHasher` produces salted, non-reversible hashes. A central authorization fallback policy protects controllers; login and Swagger remain public. The JWT signing key is supplied at runtime, not compiled into the image. Token lifetime is **60 minutes**. The React token is kept in **sessionStorage** (survives reloads in the same tab, generally not a new browser session); unlike an HttpOnly cookie, it is readable by JavaScript if XSS occurs. Protected responses returning 401 clear the session and redirect to login.

**Database:** EF migrations add hashed agent credentials and ticket escalation records, including an indexed escalation TicketId foreign key for efficient history lookup. Relationships, field lengths and delete behavior are configured in EF Core mappings. SQL read queries favor narrow projections / `AsNoTracking` where applicable.

**Production migrations:** Automatic migration/seeding at application startup is intentionally **Development-only** for this assessment. A real deployment should apply reviewed migrations through a controlled release job/process, with backups and rollout coordination, rather than letting every application instance migrate a shared production database.

## Tests and Docker verification

Run these from the repository root:

```powershell
dotnet test
npm run typecheck
npm run lint
npm run test --workspace apps/web
```

Last verified: **53 .NET tests** and **17 React tests**, all passing. Coverage includes ticket filters, SLA/triage/escalation behavior and boundaries, form rejection/409 handling, token attachment, clearing authentication on 401, and login requests without a stored token. Manual browser checks verified login, logout, and redirect/return-to-page behavior. No production load, penetration or cross-browser testing has been performed.

Docker checks performed:

| Check | Result |
| --- | --- |
| `docker compose up -d --build` | API built/running; SQL Server healthy |
| `GET http://localhost:5080/health` | **200 Healthy**; database reachable |
| `GET /api/tickets` without JWT | **401 Unauthorized** |
| `docker compose exec api whoami` | **`app`**, not root |
| Repeat build after a source change | NuGet `restore` layer shown as **CACHED** |
| API image (`northwind-support-api:latest`) | **454,675,921 bytes ≈ 454.7 MB** |
| .NET SDK image (`mcr.microsoft.com/dotnet/sdk:10.0`) | **1,303,343,059 bytes ≈ 1,303.3 MB** |

The API runtime image is approximately **848.7 MB (65.1%) smaller** than the SDK image. Build and runtime use separate .NET 10 images. The API runs on HTTP port **8080** as a non-root user; no signing secret or certificate is baked into the image.

**Fresh-volume verification:** The application is configured to migrate and seed an empty SQL database in Development. A destructive `docker compose down -v` fresh-volume test has **not been independently recorded here**; do not claim it was verified unless you run it. It deletes the database volume, so back up any data first or use an isolated temporary Compose project.

Troubleshooting: Run `docker compose logs api --tail=100` and `docker compose ps` if startup fails. Ensure `.env` has `Jwt__Key`, verify ports `1433` and `5080` are free, and check database health. Use `docker compose down` to stop containers **without removing the SQL volume**.

## AI assistance and known limitations

AI assistance was used to interpret acceptance criteria, draft code and tests, troubleshoot TypeScript imports and Docker build errors, and improve documentation. Suggestions were reviewed against the assignment and validated with compiler checks, automated tests, API calls and Docker logs. For example, the first Dockerfile omitted `Directory.Build.props`, which caused `NETSDK1013`; adding it before restore corrected the build. The first database health-check suggestion used an unavailable extension method; it was replaced with an asynchronous `IHealthCheck` backed by `CanConnectAsync`. The implemented behavior—not AI output alone—was treated as the evidence of completion.

Known limitations: This is a development assessment rather than a production-ready identity or hosting system. It intentionally omits registration, password reset, refresh tokens, MFA, roles/permissions, rate limiting, background jobs, CI/CD, frontend containerization and HTTPS termination in Docker. Production would need secret management, HTTPS at the ingress, hardened database permissions, cookie-based or other appropriately protected authentication, observability, and a controlled migration process.

## Submission

Submit the **GitHub fork URL**, the development login credentials above, and an **honest approximate time breakdown** for Tasks 1–4. Keep the distinct commits for the Task 1 defect and filtering work. If the fork is private, grant reviewers access. See `ASSIGNMENT_CANDIDATE.md` for assignment details. If the repository's submission instructions require `APPROACH.md`, submit that file separately as well.
