# BamTech — Stargate ACTS

Astronaut Career Tracking System (ACTS) technical exercise: the supplied Stargate API, enhanced, with an Angular UI to follow.

> Work in progress. The supplied starter is committed unmodified (`46b669d`), and later commits apply the fixes as reviewable diffs. Design rationale and the full API contract will be documented here before handoff.

## Prerequisites

- .NET 10 SDK (10.0.100 or later; see `global.json`)
- Node.js 24.18 or later with npm 11 (for the Angular UI in `web/`)

There is no database server to install: the API uses a local SQLite file.

## Set up and run the API

```bash
dotnet tool restore                               # installs the pinned dotnet-ef locally
dotnet build                                      # restores packages; dotnet ef needs them
dotnet ef database update --project src/StargateAPI
dotnet run --project src/StargateAPI --launch-profile https -- seed-demo-users

dotnet run --project src/StargateAPI --launch-profile https
```

In Development (the `https` launch profile), seeding creates fixed demo accounts:

| Username | Password | Role |
| --- | --- | --- |
| `admin` | `Admin!2026` | Admin |
| `reader` | `Reader!2026` | Reader |

These passwords are public and for local demos only. **They are never used outside Development:** any other environment must supply `DemoUsers:AdminPassword` and `DemoUsers:ReaderPassword` (user-secrets or the environment variables `DemoUsers__AdminPassword` / `DemoUsers__ReaderPassword`), or seeding refuses. Configured values also override the demo passwords in Development.

Seeding is idempotent: re-running it adds missing roles or accounts and leaves existing passwords unchanged. To switch an existing database to different passwords, delete `starbase.db`, run `dotnet ef database update` and seed again.

- The database file is `src/StargateAPI/starbase.db`. A relative `Data Source` in `ConnectionStrings:StarbaseApiDatabase` resolves against the API project folder, whichever folder you start it from.
- Swagger UI is available in Development at `https://localhost:7204/swagger`.
- **Fresh databases only.** The `ConvertToGuidIdentity` migration moves the starter's integer keys to UUIDv7 and refuses to run (making no changes) if the database already holds people or duties. Converting populated data is not implemented. Part of the migration (SQLite table rebuilds) runs outside a transaction; if it is interrupted, delete `starbase.db` and run `dotnet ef database update` again.

## Run the web UI

The Angular app (`web/`) is served over HTTPS and proxies `/api` and `/auth` to the API on the same origin, so the Secure, SameSite=Strict cookies work without CORS. Start the API first (above), then:

```bash
cd web
npm ci
npm run certs        # exports the ASP.NET development certificate to web/.certs (git-ignored)
npm start            # https://localhost:4200
```

If the browser warns about the certificate, trust it once with `dotnet dev-certs https --trust`. The same self-signed localhost certificate serves both the API and the UI.

```bash
npm test             # unit tests (Vitest)
npm run build        # production build to web/dist
# End-to-end check against the running API and dev server (uses your installed Chrome).
# Uses the Development admin password; set E2E_ADMIN_PASSWORD if you seeded your own.
npm run e2e
```

## Sign-in and permissions

Every API route requires a signed-in account except the three sign-in routes.

| Account | Role | Can |
| --- | --- | --- |
| `admin` | Admin | read people and duties, create and update people, add duties |
| `reader` | Reader | read people and duties |

- Sign-in uses an HttpOnly, Secure, SameSite=Strict cookie backed by a server-side session (8 hours, not extended by activity). The session is checked on every request, so **logout revokes it immediately**: a copied cookie stops working on its next use. Disabling an account or changing its security stamp also ends its sessions.
- State-changing requests need an antiforgery token: call `GET /auth/antiforgery`, then send the `XSRF-TOKEN` cookie value as the `X-XSRF-TOKEN` header on POST/PUT, including login and logout. A missing or invalid token gets 400 `csrf_failed`.
- Responses are 401 `unauthenticated` when not signed in and 403 `forbidden` when the role lacks permission; there are no login redirects. Five failed sign-ins lock an account for five minutes, and every failure returns the same generic 401.
- `GET /auth/me` returns the account, its roles and its capabilities (`People.Read`, `People.Write`, `Duties.Write`).

Signing in with curl (`-k` accepts the local development certificate):

```bash
curl -k -c jar -b jar https://localhost:7204/auth/antiforgery
XSRF=$(awk '/XSRF-TOKEN/ {print $7}' jar)
curl -k -c jar -b jar -H "X-XSRF-TOKEN: $XSRF" -H "Content-Type: application/json" \
     -d '{"userName":"admin","password":"Admin!2026"}' https://localhost:7204/auth/login
curl -k -b jar "https://localhost:7204/api/people?pageSize=10"
```

## Process logs

Successes and exceptions are stored in the `ProcessLog` table:

- **MutationAudit** rows are written by each change (create, rename, duty assignment) in the same transaction as the change itself. If the audit cannot be saved, the change rolls back.
- **RequestOutcome** rows record one outcome per HTTP request (Succeeded / Rejected / Failed / Cancelled), with status, stable error code, duration and, for failures, the exception type and stack trace. Exception messages, request bodies and personal names are never stored. Swagger and `/api/metadata` are not logged.
- Both rows for a request share `OperationId` and `TraceId`. `TraceId` is the `traceId` returned in every error response, so a reported error can be found directly. `OccurredAtUtc` is stored as Unix milliseconds.
- Request-outcome logging is best effort: if the database cannot store it, the API logs a warning to the console and the response is unaffected.

Example queries (SQLite):

```sql
-- Find the request behind an error a client reported
SELECT * FROM ProcessLog WHERE TraceId = '00-…' ORDER BY OccurredAtUtc;

-- Recent failures, newest first
SELECT datetime(OccurredAtUtc / 1000, 'unixepoch') AS At, Operation, HttpStatusCode, ErrorCode, ExceptionType
FROM ProcessLog WHERE Outcome = 'Failed' ORDER BY OccurredAtUtc DESC LIMIT 50;

-- Change history for one person
SELECT datetime(OccurredAtUtc / 1000, 'unixepoch') AS At, Operation, Message, ChangesJson
FROM ProcessLog WHERE PersonId = '…' AND Phase = 'MutationAudit' ORDER BY OccurredAtUtc;
```

The table grows with every request; there is no automatic retention or cleanup in this local exercise.

## Tests

Tests use xUnit v3 on Microsoft.Testing.Platform (enabled in `global.json`).

```bash
dotnet test
dotnet test --coverlet --coverlet-output-format cobertura --results-directory TestResults
```

- Domain rules (`tests/.../Domain`) are tested directly, without HTTP or a database.
- API tests run the real application against an isolated in-memory SQLite database per test. Concurrency tests use a temporary file database instead, because in-memory SQLite locks differently.
- Temporary database files live under `%TEMP%/stargate-tests/<run>` and are deleted when the run ends; folders left by interrupted runs are removed by the next run. Tests never touch `starbase.db`.
- The starter's defects were first captured as failing regression tests (Increment 1), which now pass. See the git history.
