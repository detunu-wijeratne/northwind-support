# Implementation Approach — Northwind Support Desk

## Overview

I completed the assignment incrementally, following the required task order. I focused on preserving the existing architecture, separating business rules from API and UI concerns, and verifying changes through automated tests and manual testing.

## Task 1 — Defect Fix and Server-Side Filtering

### Approach

I investigated why changing the ticket status filter did not immediately trigger a new API request. I traced the interaction between filter state, the ticket-list data hook, and the request dependencies.

I fixed the underlying synchronization issue rather than forcing a component reload or applying filters locally.

For server-side filtering, I extended the existing ticket query implementation to apply filters through Entity Framework Core before counting, sorting, and pagination.

### Design decisions

- Kept filtering on the database side rather than filtering returned results in React.
- Combined supplied filters using AND conditions.
- Preserved a single query-building approach so additional filters, such as SLA status, could be added without restructuring the implementation.
- Added regression and filtering tests to verify request behavior and query results.

## Task 2 — Ticket Escalation and Assignment

### Approach

I implemented automatic ticket triage during creation, including priority determination, SLA calculation, and agent assignment.

I separated the business rules into testable services instead of placing them directly inside controllers. This allowed the same SLA and assignment logic to be reused during escalation.

Ticket escalation raises priority by one level, recalculates the SLA deadline, re-evaluates assignment, and creates a persistent history record.

### Design decisions

- Used configurable SLA values rather than hard-coded deadline calculations.
- Applied category flags to enforce Critical priority or specialist assignment requirements.
- Selected eligible agents deterministically based on workload and capacity.
- Allowed tickets to remain unassigned when no eligible agent was available.
- Stored escalation history in a dedicated database table with an EF Core migration.
- Derived SLA status instead of storing potentially outdated values.

### Verification

I tested SLA boundaries, assignment eligibility, escalation rules, and rejected escalation requests. The React tests also cover SLA indicators, client-side validation, successful escalation, and HTTP 409 error handling.

## Task 3 — Login and Authentication

### Approach

I added authentication for existing agents without introducing a separate registration system.

I extended the Agent entity with a password-hash field and used ASP.NET Core's `PasswordHasher` to generate salted password hashes for development accounts.

I implemented JWT login and current-user endpoints, then configured a fallback authorization policy to protect existing API endpoints centrally.

On the frontend, I added a login page, authentication context, token handling, route protection, and logout functionality.

### Design decisions

- Used the existing Agent entity because authentication is limited to existing support agents.
- Used `PasswordHasher` instead of implementing custom cryptography.
- Used a centrally enforced authorization policy rather than repeating authorization attributes on every controller.
- Stored the JWT in browser `sessionStorage`. This supports refreshes within a browser tab, but JavaScript-accessible tokens remain exposed to XSS risks.
- Retrieved the escalation actor from the authenticated JWT instead of trusting a client-provided name.
- Kept the JWT signing key in runtime configuration rather than committing it to source control.

### Verification

I manually verified successful login, invalid credentials, protected API access, the current-agent endpoint, and JWT-based escalation.

I also verified login and logout in the browser, protected-route redirection, and returning to the originally requested page after authentication.

Frontend authentication tests cover attaching a token, clearing a session after HTTP 401, and sending unauthenticated login requests.

## Task 4 — Docker Deployment

### Approach

I created a multi-stage .NET 10 Dockerfile using the SDK image for compilation and publishing, followed by an ASP.NET Core runtime image.

I extended the existing Docker Compose configuration to start SQL Server and the API together. The API connects to SQL Server using the Compose service name.

Development startup applies EF Core migrations and seeds demonstration data.

I also added a public database-aware health endpoint.

### Design decisions

- Used a multi-stage build to exclude the SDK and build tools from the runtime image.
- Copied project and shared configuration files before source code to enable cached dependency restoration.
- Ran the API as the non-root `app` user on port 8080.
- Supplied database configuration and JWT signing keys through runtime environment variables.
- Reused the existing SQL Server health check and persistent named volume.
- Kept the React frontend outside the Docker deployment because the assignment only requires Dockerizing the API.

### Verification

- `docker compose up -d --build` successfully built and started the containers.
- `GET /health` returned HTTP 200 with `Healthy`.
- Requests to protected endpoints without a token returned HTTP 401.
- `docker compose exec api whoami` returned `app`.
- Rebuilding reused Docker's cached NuGet restore layer.
- The API image size was approximately 454.7 MB, compared with approximately 1,303.3 MB for the .NET SDK image, a reduction of about 65.1%.

A destructive fresh-volume migration test was not performed against the existing development database because it contains data that should be preserved.

## Testing and Quality

The final recorded automated test runs passed:

- Backend: 53 tests.
- Frontend: 17 tests.
- Total: 70 tests.

TypeScript type checking and ESLint also passed without errors.

I supplemented these checks with manual browser, API, and Docker verification.

## AI-Assisted Development

I used ChatGPT to assist with understanding requirements, reviewing implementation approaches, generating initial code suggestions, debugging compiler and Docker errors, and preparing verification commands.

I reviewed and adapted the suggested changes to the existing project rather than treating every generated answer as final.

For example, the first Docker build failed because the shared `Directory.Build.props` file was not included before dependency restoration. I corrected the Dockerfile and rebuilt it successfully.

During health-check development, the suggested `AddDbContextCheck` extension was unavailable without an additional package. I replaced that approach with a custom asynchronous health check using the existing EF Core database context.

I verified the implementation through automated tests, TypeScript and lint checks, API requests, browser interactions, and Docker builds.

## Known Limitations and Future Improvements

The implementation focuses on the scope of the assignment.

It does not include user registration, password recovery, MFA, OAuth/SSO, refresh tokens, role-based authorization, notification services, CI/CD, or production deployment.

For production, I would introduce secret management, HTTPS termination, a stronger token-handling strategy, operational monitoring, and a controlled database-migration process separate from normal application startup.