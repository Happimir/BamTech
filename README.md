# BamTech — Stargate ACTS

Astronaut Career Tracking System (ACTS) technical exercise: the supplied Stargate API, corrected and extended, plus an Angular UI.

- **API** (`src/`, .NET 10, EF Core + SQLite). It is split into onion-architecture layers (Domain, Application, Infrastructure, API) whose references point inward only. All required operations work. The seven rules are enforced in one tested domain operation, and starter defects are fixed. Writes are transactional and race-safe. Success and failure logs are stored in the database. Sign-in uses revocable sessions with Admin and Reader roles.
- **UI** (`web/`, Angular 22 + Angular Material). A people directory with search and server paging; person detail with the current assignment and a newest-first duty timeline. Sign-in and sign-out. Loading, empty and error states. Responsive from 360px; checked with axe-core.
- **Evidence:** 132 API tests at 93.9% line coverage, including architecture tests for the layer rules; 79 UI unit tests; 19 browser tests against the real API.

The supplied starter is committed unmodified (`46b669d`), and every later fix is a reviewable diff.

## Contents

- [Prerequisites](#prerequisites) · [Set up and run the API](#set-up-and-run-the-api) · [Run the web UI](#run-the-web-ui)
- [Demonstration](#demonstration)
- [Project structure](#project-structure)
- [Design decisions](#design-decisions): identity and rule 1, the rules, policies, fixed defects
- [API](#api) · [Sign-in and permissions](#sign-in-and-permissions) · [Process logs](#process-logs)
- [Tests and coverage](#tests-and-coverage)
- [Limitations and deferred scope](#limitations-and-deferred-scope)

## Prerequisites

- .NET 10 SDK (10.0.100 or later; see `global.json`)
- Node.js 24.18 or later with npm 11 (for the Angular UI in `web/`)

There is no database server to install: the API uses a local SQLite file.

## Set up and run the API

```bash
dotnet tool restore                               # installs the pinned dotnet-ef locally
dotnet build                                      # restores packages; dotnet ef needs them
dotnet ef database update --project src/StargateAPI.Infrastructure --startup-project src/StargateAPI
dotnet run --project src/StargateAPI --launch-profile https -- seed-demo-users

dotnet run --project src/StargateAPI --launch-profile https
```

In Development (the `https` launch profile), seeding creates fixed demo accounts:

| Username | Password | Role |
| --- | --- | --- |
| `admin` | `Admin!2026` | Admin |
| `reader` | `Reader!2026` | Reader |

These passwords are public and for local demos only. **They are never used outside Development:** any other environment must supply `DemoUsers:AdminPassword` and `DemoUsers:ReaderPassword` (user-secrets or the environment variables `DemoUsers__AdminPassword` / `DemoUsers__ReaderPassword`), or seeding refuses. Configured values also override the demo passwords in Development.

Seeding is idempotent: re-running it adds missing roles or accounts and leaves existing passwords unchanged. To switch an existing database to different passwords, delete `starbase.db`, run the `dotnet ef database update` command above and seed again.

- The database file is `src/StargateAPI/starbase.db`. A relative `Data Source` in `ConnectionStrings:StarbaseApiDatabase` resolves against the API project folder, whichever folder you start it from.
- Swagger UI is available in Development at `https://localhost:7204/swagger`.
- **Fresh databases only.** The `ConvertToGuidIdentity` migration moves the starter's integer keys to UUIDv7 and refuses to run (making no changes) if the database already holds people or duties. Converting populated data is not implemented. Part of the migration (SQLite table rebuilds) runs outside a transaction; if it is interrupted, delete `starbase.db` and run the `dotnet ef database update` command above again.

## Run the web UI

The Angular app (`web/`) is served over HTTPS and proxies `/api` and `/auth` to the API on the same origin, so the Secure, SameSite=Strict cookies work without CORS. Start the API first (above), then:

```bash
cd web
npm ci
npm run certs        # exports the ASP.NET development certificate to web/.certs (git-ignored)
npm start            # https://localhost:4200
npm run seed:sample  # optional: fictional sample people and duty histories, via the API
```

If the browser warns about the certificate, trust it once with `dotnet dev-certs https --trust`. The same self-signed localhost certificate serves both the API and the UI.

The sample data is created through the API as `admin`, so it goes through the same validation as the UI. It covers every state the UI shows: people with no assignment, active, retired and reactivated careers, namesakes, a long name and title, a long history, and enough people for two pages. It runs only once: if its first person already exists, it does nothing.

```bash
npm test             # unit tests (Vitest)
npm run build        # production build to web/dist
# End-to-end checks against the running API and dev server (uses your installed Chrome).
# Seeds the sample data first if missing. Uses the Development admin password;
# set E2E_ADMIN_PASSWORD, E2E_READER_PASSWORD (and ACTS_ADMIN_PASSWORD for the seed)
# if you seeded your own. Includes axe-core accessibility checks (WCAG 2.1 A/AA).
npm run e2e
```

## Demonstration

With the API running, `cd web && npm run demo:api` walks through the rules against the real API with fresh, uniquely named people, printing each request and its outcome:

1. A new person has no astronaut records (rule 2).
2. A first and then a second duty: each becomes current, and the previous one ends the day before (rules 3–5).
3. `retired` (any case) retires the person; the career ends the day before (rules 6–7). Retiring again is refused with `already_retired`.
4. Reactivation: a new active duty clears the current career end, and the retirement interval stays in the history.
5. A start date that is not after the current duty is refused with `chronology_conflict`, and nothing changes.
6. Two people with the same name: by-name reads and writes are refused with `ambiguous_name` (nothing written); search returns both with their IDs, and the duty is added by ID.
7. The `traceId` from the refused request, with the SQL that finds its `ProcessLog` row and the person's change history.

In the UI (`npm run seed:sample` first), sign in as `admin` or `reader`. Then:

- search "Gr" and open **Grace Okonkwo**: a reactivated career, with no current career end, the 2020–2024 retirement kept in the timeline, and a "Returned to active duty" note;
- open **Harold Jensen** to see a retired career end;
- open **Priya Raman** to see a person with no assignments;
- search "Alex Morgan" to see two namesakes told apart by ID;
- page through the directory, open a person, then use Back to people to return to the same page and search.

## Project structure

The API is split into onion-architecture layers. Every reference points inward, so the career rules never depend on a framework or a database:

```
src/StargateAPI                 API host and composition root
  │                             controllers, HTTP middleware, sign-in and security, Program.cs
  ├──► StargateAPI.Application  use cases: MediatR commands and queries, DTOs, logging context,
  │       │                     and the persistence abstractions they need (IStargateDbContext,
  │       │                     IWriteTransactions, IPersistenceErrors, IRequestOutcomeLog)
  │       └──► StargateAPI.Domain   entities and career rules (CareerTransitions, NameNormalizer,
  │                                 Result/AppError); no package or project references
  └──► StargateAPI.Infrastructure   EF Core + SQLite: StargateContext, entity configurations,
          │                         migrations, SQLite transactions and error codes;
          └──► Application, Domain  ASP.NET Core Identity accounts and sessions
```

- **Domain** has no dependencies at all.
- **Application** may use EF Core's general API (sets, LINQ), but never SQLite. Two SQLite specifics sit behind interfaces implemented in Infrastructure: the write lock taken at the start of a transaction, and unique-violation and busy error codes. Changing the database provider would mean changing Infrastructure only.
- **The API project** is the composition root. Business controllers only send MediatR requests. The sign-in controller and session validation use Identity from Infrastructure directly, because they are ASP.NET plumbing.
- **The rule is enforced, not just intended:** `ArchitectureTests` checks the compiled assemblies. Domain references nothing; Application references only Domain and never SQLite; Infrastructure never references the API; business controllers never depend on an Infrastructure type. Each test was shown to fail when its rule was broken.

## Design decisions

### Identity: rule 1 replaced, with the recruiter's agreement

Rule 1 ("a person is uniquely identified by their name") is deliberately replaced by stable identifiers. Names change and can be shared; a career history must stay attached to the same person regardless. I raised this with the recruiter before changing it:

> "I would ignore that ask. Design it as you see fit based on what you see as best practices. I would follow that line of thought throughout."

and, on remaining ambiguities (including the .NET 10 target):

> "You are free to do as you please across the board. … some ambiguity is there by design. That is part of the exercise."

| | Starter | Now |
| --- | --- | --- |
| Identity | Integer key; the name was treated as unique, but nothing enforced it | UUIDv7 (`Guid.CreateVersion7()`), generated once by the server and never changed |
| Same name, different people | Possible by accident (the duplicate-name check was never registered) | Supported deliberately: each is a separate person with its own ID and history |
| Name-based routes (`/Person/{name}`, `/AstronautDuty`, `PUT /Person/by-name`) | First match, or SQL errors | Still supported: exactly one match is used, none gives 404, and several give **409 `ambiguous_name`** with nothing written |
| Selecting a person | By name | By ID (`/api/people/{id}`); search returns every match with its ID |
| Matching | Exact SQL text | Trimmed, Unicode-normalized, case-insensitive; accents and inner spaces are kept |

A version token (`version` / `expectedVersion`) protects updates against stale writes. Renaming by name never creates a person: a retried rename of a name that no longer exists gets 404.

### The rules

| Rule | How it is enforced |
| --- | --- |
| 2: no assignments, no astronaut records | Creating a person creates no detail or duty; the API returns `astronaut: null` |
| 3: one current rank/title/start | One summary row per person, updated in the same transaction as each new duty |
| 4: the current duty has no end date | At most one open duty per person, also enforced by a unique partial index |
| 5: the previous duty ends the day before the next starts | Start dates must strictly increase per person; the previous duty is closed in the same transaction |
| 6: RETIRED means retired | The title is canonicalized (`retired` → `RETIRED`); status is derived from the current title |
| 7: career ends the day before retirement | While retired, career end = retirement start − 1 day; it clears on reactivation |

All of these live in one pure domain operation (`CareerTransitions.AssignDuty`), which validates before changing anything. The handler runs it inside an immediate SQLite write transaction, so concurrent appends cannot interleave.

### Policies where the assignment is silent

- **Reactivation is allowed.** A retired person can take a new active duty; the current career end clears and the retirement stays in the history. Career start stays the first active date.
- **Retirement must follow an active duty.** Retirement as a first duty (`first_duty_retirement`) or while already retired (`already_retired`) is refused.
- **No future-dated duties.** "Current" means effective now; the business date is UTC (`GET /api/metadata`).
- **Dates are calendar dates** (`YYYY-MM-DD`), never time-zone converted; operation timestamps are UTC.
- **History is append-only.** The only change to an existing duty is closing its end date. Corrections or backfills would need a separate operation.
- **Invalid input is 400; a valid request that conflicts with the current state is 409.** Rejections never partially change data.

### Starter defects fixed

Each was first captured as a failing test in Increment 1 and now passes.

| Starter behaviour | Now |
| --- | --- |
| `GET /AstronautDuty/{name}` returned the person, not their duties | Returns the duty history, newest first |
| A name with an apostrophe caused a 500 (SQL built by string concatenation) | Matched literally; all queries are parameterized |
| The name `' OR 1=1 OR '` returned another person (SQL injection) | 404 |
| An unknown name returned 200 | 404 `not_found` |
| A duty for a missing person threw an unhandled exception | 404 |
| Another person's identical title and date blocked a duty | Checks are per person |
| A first duty of RETIRED was accepted, with career end = start | 409 `first_duty_retirement` |
| An earlier start date was accepted, inverting the history | 409 `chronology_conflict`, nothing changed |
| A repeated start date threw an unhandled exception | 409 `chronology_conflict` |
| The duplicate-name check never ran (its preprocessor was not registered) | Namesakes are supported deliberately; ambiguity is refused |
| Model/migration drift stopped database creation on EF Core 9+; vulnerable transitive packages; 6 nullable warnings | Fixed; `dotnet list package --vulnerable` is clean; 0 build warnings |

## API

JSON is camelCase, IDs are UUID strings and dates are `YYYY-MM-DD`. Errors are ProblemDetails with a stable `code` and a `traceId`. Branch on `code`, never on message text. Swagger (Development) documents every route.

| Route | Permission | Purpose |
| --- | --- | --- |
| `GET /api/people?page&pageSize&name` | People.Read | Paged directory (1-based page, size 1–100, default 25); `name` is a case-insensitive prefix |
| `GET /api/people/{id}` | People.Read | One person: summary and supplied name parts |
| `GET /api/people/{id}/duties` | People.Read | The person and their duties, newest first, as one consistent snapshot |
| `GET /api/people/{id}/duties/{dutyId}` | People.Read | One duty |
| `POST /api/people` `{name, firstName?, middleName?, lastName?}` | People.Write | Create (namesakes allowed); 201 with Location |
| `PUT /api/people/{id}` `{name, …parts, version}` | People.Write | Replace names; a stale `version` gets 409 |
| `POST /api/people/{id}/duties` `{rank, dutyTitle, dutyStartDate, expectedVersion?}` | Duties.Write | Add a duty; 201 with Location |
| `GET /api/metadata` | People.Read | Business date and time zone (UTC) |
| `GET /Person`, `GET /Person/{name}`, `POST /Person`, `PUT /Person/by-name?name=` | as above | The assignment's name-based operations, kept for compatibility |
| `GET /AstronautDuty/{name}`, `POST /AstronautDuty` `{name, rank, dutyTitle, dutyStartDate}` | as above | Name-based duty history and duty creation |

Error codes: `validation`, `not_found`, `ambiguous_name`, `chronology_conflict`, `first_duty_retirement`, `already_retired`, `concurrency_conflict`, `version_required`, `service_unavailable` (503, retry later), `unauthenticated` (401), `forbidden` (403), `csrf_failed`, `invalid_credentials`, `bad_request`, `internal_error`.

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
- The UI hides what a role cannot do and guards its routes, but only for usability: the API enforces every permission.

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
- **RequestOutcome** rows record one outcome per HTTP request (Succeeded / Rejected / Failed / Cancelled), with status, stable error code, duration, the signed-in account's ID and, for failures, the exception type and stack trace. Exception messages, request bodies and personal names are never stored. Swagger and `/api/metadata` are not logged.
- Both rows for a request share `OperationId` and `TraceId`. `TraceId` is the `traceId` returned in every error response, so a reported error can be found directly. `OccurredAtUtc` is stored as Unix milliseconds.
- Request-outcome logging is best effort: if the database cannot store it, the API logs a warning to the console and the response is unaffected.

Example queries (SQLite). IDs are stored as uppercase text, so compare them with `upper(...)`:

```sql
-- Find the request behind an error a client reported
SELECT * FROM ProcessLog WHERE TraceId = '00-…' ORDER BY OccurredAtUtc;

-- Recent failures, newest first
SELECT datetime(OccurredAtUtc / 1000, 'unixepoch') AS At, Operation, HttpStatusCode, ErrorCode, ExceptionType
FROM ProcessLog WHERE Outcome = 'Failed' ORDER BY OccurredAtUtc DESC LIMIT 50;

-- Change history for one person (paste the ID as returned by the API)
SELECT datetime(OccurredAtUtc / 1000, 'unixepoch') AS At, Operation, Message, ChangesJson
FROM ProcessLog WHERE PersonId = upper('…') AND Phase = 'MutationAudit' ORDER BY OccurredAtUtc;
```

The table grows with every request; there is no automatic retention or cleanup in this local exercise.

## Tests and coverage

API tests use xUnit v3 on Microsoft.Testing.Platform (enabled in `global.json`).

```bash
dotnet test
# Coverage, excluding generated migrations; fails the run below 50% line coverage:
dotnet test --coverlet --coverlet-output-format cobertura \
  --coverlet-exclude-by-file "**/Migrations/*.cs" \
  --coverlet-threshold 50 --coverlet-threshold-type line --results-directory TestResults/coverage
```

Measured on 2026-10-13, after the layer split: **132 tests pass; line coverage 93.9% (1457/1552), branch coverage 82.7% (340/411)**. By layer: Domain 98.7%, Application 96.5%, API 92.9%, Infrastructure 85.7%. This covers all handwritten API code, including authentication, sessions and logging; only generated migrations are excluded, by the file filter above. The rule and transition code is at 90–100%. The lowest file is the demo-account seeder: its command-line path is verified manually.

Coverage is evidence, not the goal. Every guard test was shown to fail by temporarily breaking what it protects. The most important tests:

- **`CareerTransitions.AssignDuty`** (`CareerTransitionsTests`): every rule, including retirement, reactivation, leap days and year boundaries. A rejected duty changes nothing.
- **`AssignDutyHandler`** (`DutyAssignmentApiTests`, concurrency tests): the transaction and stale-version checks. Concurrent identical or out-of-order appends leave one open duty and a contiguous history.
- **`PersonLookup`** and the name routes (`PersonReadTests`, `PersonWriteApiTests`): an ambiguous name never selects a person or writes anything; a retried rename never creates a person.
- **Authentication** (`AuthTests`): every route has a policy; Reader is denied all six write routes with nothing written; a replayed cookie after logout is rejected; CSRF is required.
- **Process logging** (`ProcessLoggingTests`): if the audit cannot be saved, the change rolls back; a logging outage never fails a request.
- **`NameNormalizer`**, **`ApiExceptionHandler`**: matching correctness; no exception details leak to clients.
- **Layering** (`ArchitectureTests`) and **`SqliteWriteTransactions`** (`SqliteTransactionBehaviorTests`): the dependency rule holds; the write transaction the handlers rely on takes the database lock immediately.

Test isolation:

- Domain rules (`tests/.../Domain`) are tested directly, without HTTP or a database.
- API tests run the real application against an isolated in-memory SQLite database per test. Concurrency tests use a temporary file database instead, because in-memory SQLite locks differently.
- Temporary database files live under `%TEMP%/stargate-tests/<run>` and are deleted when the run ends. Folders left by interrupted runs are removed by the next run. Tests never touch `starbase.db`.

The UI has 79 unit tests (`npm test`: stores, guards, date formatting, timeline, return-URL safety) and 19 browser tests (`npm run e2e`). The browser tests cover sign-in and session revocation, search and paging, person detail states, the Reader experience, accessibility and 200% zoom. See [Run the web UI](#run-the-web-ui).

## Limitations and deferred scope

**Security (local demo):**
- The demo passwords are public and work only in Development.
- There is no sign-in rate limiting beyond account lockout, and no timing equalization between unknown accounts and wrong passwords.
- HTTPS uses the local development certificate.

**Retrying writes:**
- Nothing is retried automatically, and there are no idempotency keys.
- Retrying a duty that may have succeeded is safe: the repeated start date gets 409 and nothing is written. For stricter guarantees, send `expectedVersion`.
- Retrying a create after a timeout can create a namesake, because duplicate names are allowed. Search by name before retrying.

**Data:**
- The migrations support fresh databases only (see above).
- The process log has no retention.
- A request body over 32 KiB gets 413 with the generic `bad_request` code.

**Not built:**
- Admin create, rename and add-duty forms in the UI (optional in the plan). Writes are done through the API: Swagger, `npm run demo:api` or curl.
- Hosting the production UI build; the UI runs through the Angular dev server.
- Dark mode.
- Converting a populated database.
- Integration with the external data source the assignment mentions (no interface for it was supplied).

**Known untested paths:**
- The Cancelled and escaped-exception branches of request-outcome logging.
- A rename racing a duty append. Both take the same write lock, so they serialize by construction.
- Lock contention under load.
