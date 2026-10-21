# BamTech — Stargate ACTS

Astronaut Career Tracking System (ACTS) technical exercise: the supplied Stargate API, corrected and extended, plus an Angular UI.

- **API** (`src/`, .NET 10, GraphQL with Hot Chocolate 16, EF Core + SQLite). It is split into onion-architecture layers (Domain, Application, Infrastructure, API) whose references point inward only. All required operations work, as GraphQL queries and mutations on one `/graphql` endpoint, with business errors returned as typed data. The seven rules are enforced in one tested domain operation, and starter defects are fixed. Writes are transactional and race-safe. Success and failure logs are stored in the database. Sign-in uses revocable sessions with Admin and Reader roles.
- **UI** (`web/`, Angular 22 + Angular Material, Apollo Angular with types generated from the schema). A public introduction page; a people directory with search and server paging; person detail with the current assignment above a newest-first duty timeline. Admin dialogs to add people, rename them and add duties, including retirement. Sign-in and sign-out. Loading, empty and error states. Responsive from 360px; checked with axe-core.
- **Evidence:** 162 API tests at 92.3% line coverage, including architecture tests for the layer rules and a schema snapshot; 119 UI unit tests; 25 browser tests against the real API.

> **This branch (`graphql`) replaces the REST API with GraphQL.** The submitted REST version is on `main`. Business logic, rules, transactions and logging are the same; what changed is the API's edge (see [API](#api)) and the UI's data access.

The supplied starter is committed unmodified (`46b669d`), and every later fix is a reviewable diff.

## Where each task is

| Assignment task | Where | Proven by |
| --- | --- | --- |
| Generate the database | `dotnet ef database update` ([set-up](#set-up-and-run-the-api)); migrations in `src/StargateAPI.Infrastructure/Persistence/Migrations` | `MigrationTests`, `StarterBaselineTests` |
| The five API operations | GraphQL fields in `src/StargateAPI/GraphQL`: `personByName`, `upsertPersonByName`, `assignDuty` with `person: { name }` (by name); `people`, `person`, `createPerson`, `updatePerson`, `assignDuty` with `person: { id }` (by ID) | `PersonReadTests`, `PersonWriteApiTests`, `PeopleListApiTests`, `DutyAssignmentApiTests` |
| Enforce the rules | `CareerTransitions.AssignDuty` (Domain), backed by database constraints; rule 1 is [a deliberate exception](#identity-stable-ids-instead-of-unique-names-rule-1) | `CareerTransitionsTests`, `DutyAssignmentApiTests` |
| Defensive coding | Validated inputs (`DutyAssignmentInput`, `PersonNameInput`), a typed schema, write-locked transactions, `GraphQLErrorFilter` and `ApiExceptionHandler` | `GraphQLErrorFilterTests`, `ApiExceptionHandlerTests`, the concurrency tests, `AuthTests` |
| Unit tests, >50% coverage | `tests/StargateAPI.Tests` | 162 tests, 92.3% of lines; the coverage run fails below 50% |
| Process logging in the database | `ProcessLog`, `AuditTrail`, `RequestOutcomeLoggingMiddleware` | `ProcessLoggingTests` |
| UI (encouraged) | `web/` | 119 unit tests, 25 browser tests |

## Contents

- [Prerequisites](#prerequisites) · [Set up and run the API](#set-up-and-run-the-api) · [Run the web UI](#run-the-web-ui)
- [Demonstration](#demonstration)
- [Project structure](#project-structure)
- [Design decisions](#design-decisions): identity and rule 1, the rules, policies, the external feed, fixed defects
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

# One-time setup: creates the demo accounts, then exits (it does not start the server)
dotnet run --project src/StargateAPI --launch-profile https -- seed-demo-users

# Start the API (run this whenever you want the app running)
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
- The **Nitro** GraphQL IDE is available in Development at `https://localhost:7204/graphql`: the schema browser with every field's description, and an editor for operations. The endpoint needs a signed-in session, so sign in first (through the hosted UI on the same origin, below), then open it. Operations are POSTs and need the antiforgery header: in Nitro's connection settings, add the header `X-XSRF-TOKEN` with the value of the `XSRF-TOKEN` cookie (browser dev tools → Application → Cookies).
- **Fresh databases only.** The `ConvertToGuidIdentity` migration moves the starter's integer keys to UUIDv7 and refuses to run (making no changes) if the database already holds people or duties. Converting populated data is not implemented. Part of the migration (SQLite table rebuilds) runs outside a transaction; if it is interrupted, delete `starbase.db` and run the `dotnet ef database update` command above again.

## Run the web UI

The Angular app (`web/`) is served over HTTPS and proxies `/graphql` and `/auth` to the API on the same origin, so the Secure, SameSite=Strict cookies work without CORS. Start the API first (above), then:

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
npm run codegen      # regenerate typed GraphQL operations after a schema or .graphql change
npm run build        # production build to web/dist
# End-to-end checks against the running API and dev server (uses your installed Chrome).
# Seeds the sample data first if missing. Uses the Development admin password;
# set E2E_ADMIN_PASSWORD, E2E_READER_PASSWORD (and ACTS_ADMIN_PASSWORD for the seed)
# if you seeded your own. Includes axe-core accessibility checks (WCAG 2.1 A/AA).
npm run e2e
```

### Or: the built UI served by the API (no Node.js at run time)

The dev server above is the development flow. To view the app from a single process, build the UI once and start the API alone:

```bash
cd web && npm ci && npm run build && cd ..   # output: web/dist/web/browser
dotnet run --project src/StargateAPI --launch-profile https   # https://localhost:7204
```

- The API serves the UI from the folder set in `Spa:RootPath` (`appsettings.json`, relative to `src/StargateAPI`). If that folder has no `index.html`, the API runs exactly as before, with no UI.
- Deep links such as `/people/{id}` return `index.html` and the Angular router takes over. Paths under `/graphql` and `/auth` never do, so they keep the API's own responses.
- Page and asset loads are not written to the process log, which records API operations only.
- The browser tests run against it with `E2E_BASE_URL=https://localhost:7204 npm run e2e`.
- Rebuild the UI after changing it; the API serves whatever is in the folder.

## Demonstration

With the API running, `cd web && npm run demo:api` walks through the rules against the real API with fresh, uniquely named people, printing each GraphQL operation and its outcome:

1. A new person has no astronaut records (rule 2).
2. A first and then a second duty: each becomes current, and the previous one ends the day before (rules 3–5).
3. `retired` (any case) retires the person; the career ends the day before (rules 6–7). Retiring again is refused with `already_retired`.
4. Reactivation: a new active duty clears the current career end, and the retirement interval stays in the history.
5. A start date that is not after the current duty is refused with `chronology_conflict`, and nothing changes.
6. Two people with the same name: by-name reads and writes are refused with `ambiguous_name` (nothing written); search returns both with their IDs, and the duty is added by ID.
7. The trace ID of the refused request (its `traceresponse` header), with the SQL that finds its `ProcessLog` row and the person's change history.

In the UI (`npm run seed:sample` first), open https://localhost:4200 (or https://localhost:7204 with the built UI): an introduction to ACTS for visitors who are not signed in. Choose **Sign in to ACTS** and sign in as `admin` or `reader`. Then:

- search "Gr" and open **Grace Okonkwo**: a reactivated career, with no current career end, the 2020–2024 retirement kept in the timeline, and a "Returned to active duty" note;
- open **Harold Jensen** to see a retired career end;
- open **Priya Raman** to see a person with no assignments;
- search "Alex Morgan" to see two namesakes told apart by ID;
- page through the directory, open a person, then use Back to people to return to the same page and search.

As `admin` (a `reader` sees none of these controls):

- **Add person** on the directory: a required display name and optional first, middle and last names. A name built from the parts is offered, never applied by itself. You land on the new record.
- **Add duty** on a record: rank, title and start date. The dialog previews what the server derives: "Flight Director ends May 31, 2026", or for **Retire**, "Career ends …". Retire is offered only while a career is active. For a retired person, a new duty explains the reactivation. The latest allowed date is the server's business date (the `businessCalendar` query, UTC).
- **Rename** on a record: the ID and history stay the same.
- **Conflicts:** if the person changed after the dialog opened (try renaming them in a second tab), saving is refused with the draft kept. **Reload** shows the latest record, and you save again yourself; nothing is resent automatically. If a save's outcome is unknown (no response), the dialog reloads the record for review, and an unconfirmed create says "Creation may have succeeded" and links to a directory search before offering **Create anyway**.

## Project structure

The API is split into onion-architecture layers. Every reference points inward, so the career rules never depend on a framework or a database:

```
src/StargateAPI                 API host and composition root
  │                             GraphQL schema and resolvers (GraphQL/), schema.graphql,
  │                             the sign-in controller, HTTP middleware, security, Program.cs,
  │                             optional hosting of the built UI (Hosting/SpaHosting)
  ├──► StargateAPI.Application  use cases: MediatR commands and queries, DTOs, logging context,
  │       │                     services the write handlers share (IAuditTrail, IAstronautCareers),
  │       │                     and the persistence abstractions they need (IStargateDbContext,
  │       │                     IWriteTransactions, IPersistenceErrors, IRequestOutcomeLog)
  │       └──► StargateAPI.Domain   entities and career rules (CareerTransitions, NameNormalizer,
  │                                 Result/AppError); no package or project references
  └──► StargateAPI.Infrastructure   EF Core + SQLite: StargateContext, entity configurations,
          │                         migrations, SQLite transactions and error codes;
          └──► Application, Domain  ASP.NET Core Identity accounts and sessions
```

- **Domain** has no dependencies at all. It holds the business rules, as plain code tested without a database: career transitions, input validation, name matching, and the person's own invariants (version checks, name replacement).
- **Handlers are thin orchestrators.** Each validates, takes the write lock, loads, applies the domain rules, and saves the change with its audit. What several handlers share is a service with an interface:
  - `IAuditTrail` stages the audit row that commits with the change;
  - `IAstronautCareers` loads the career state and stages a new duty in the order the one-open-duty index needs.

  Logic used by only one handler stays in that handler instead of moving behind an interface with a single caller.
- **Application** may use EF Core's general API (sets, LINQ), but never SQLite. Two SQLite specifics sit behind interfaces implemented in Infrastructure: the write lock taken at the start of a transaction, and unique-violation and busy error codes. Changing the database provider would mean changing Infrastructure only.
- **The API project** is the composition root. GraphQL resolvers only send MediatR requests, the same ones the REST controllers on `main` sent, so the use cases did not change with the API style. The sign-in controller and session validation use Identity from Infrastructure directly, because they are ASP.NET plumbing.
- **The rule is enforced, not just intended:** `ArchitectureTests` checks the compiled assemblies. Domain references nothing; Application references only Domain and never SQLite; Infrastructure never references the API; GraphQL resolvers never depend on an Infrastructure or EF Core type; the sign-in controller is the only controller. Each test was shown to fail when its rule was broken.

The Angular app (`web/src/app`) is grouped by feature, and within a feature by role. Each spec sits beside the file it tests:

```
app.ts, app.config.ts, app.routes.ts   shell, providers, lazy-loaded routes
core/                                  app-wide, used by every feature
  data-access/                         API models and transport (PeopleApi, AuthApi)
    graphql/                           Apollo setup, shared fragments, generated operation types
  auth/                                AuthStore and route guards
  http/                                error classification (api-errors), session-expiry interceptor
  utils/                               calendar dates, safe return URLs
shared/                                used by more than one feature
  ui/status-badge/                     the Active / Retired / Not assigned badge
  utils/                               personStatus
features/
  people.routes.ts                     the signed-in routes, with their GraphQL client
  people/                              the directory
    people-page/                       the page component
    data-access/                       PeopleStore (URL-driven paging and search), people.graphql
    utils/                             query parsing, table rows
  person-detail/                       one person's record
    person-detail-page/                the page component
    data-access/                       PersonDetailStore (one person-and-duties snapshot), person-detail.graphql
    utils/                             timeline, return to the directory
  admin/                               write dialogs (Admin only)
    person-dialog/, duty-dialog/       one folder per dialog
    shared/                            write-failure handling and dialog styles
  status-pages/                        forbidden, not found, unavailable
  landing/, login/                     single-component pages
```

## Design decisions

The assignment leaves several points open or in tension with each other, and some ambiguity is part of the exercise. Where it is silent or a literal reading would weaken the system, I followed established practice for this kind of record-keeping system. Each such choice is documented below with its reasoning, and enforced by tests.

### Identity: stable IDs instead of unique names (rule 1)

Rule 1 ("a person is uniquely identified by their name") is deliberately not enforced. Two separate questions sit behind it:

- **How records are keyed.** Every person has a stable UUIDv7 key, which their duties reference. That alone keeps a history attached through a rename, and it would hold even if names were unique.
- **Whether two people may share a name.** This is the actual departure from rule 1. People do share names. A system of record that refuses the second John Smith either loses that person or forces a misspelling, so namesakes are allowed, each with their own ID and history.

The cost is that a name no longer always identifies one person. The assignment's operations all work by name, so each one refuses an ambiguous name (`AmbiguousNameError`, nothing written) instead of guessing, and selecting the person by ID settles it. If the business confirms rule 1 literally, enforcing it is a small change: a unique index on the normalized name, and a conflict error when a create would duplicate one.

Neither choice solves the external feed: a source that identifies people only by name cannot tell namesakes apart either way. The real fix is a stable person ID from the source system, which the assignment does not supply (see [Ingesting the external feed](#ingesting-the-external-feed)).

| | Starter | Now |
| --- | --- | --- |
| Identity | Integer key; the name was treated as unique, but nothing enforced it | UUIDv7 (`Guid.CreateVersion7()`), generated once by the server and never changed |
| Same name, different people | Possible by accident (the duplicate-name check was never registered) | Supported deliberately: each is a separate person with its own ID and history |
| Name-based operations (`personByName`, `upsertPersonByName`, `assignDuty` with `person: { name }`) | First match, or SQL errors | Still supported: exactly one match is used, none is a `NotFoundError`, and several are an **`AmbiguousNameError`** with nothing written |
| Selecting a person | By name | By ID (`person(id:)`, `person: { id }`); search returns every match with its ID |
| Matching | Exact SQL text | Trimmed, Unicode-normalized, case-insensitive; accents and inner spaces are kept |

A version token (`version` / `expectedVersion`) protects updates against stale writes. Renaming by name never creates a person: a retried rename of a name that no longer exists is a `NotFoundError`. Names are stored in Unicode NFC form, so re-sending the same name with its accents encoded differently is not a change.

### The rules

| Rule | How it is enforced |
| --- | --- |
| 2: no assignments, no astronaut records | Creating a person creates no detail or duty; the API returns `astronaut: null` |
| 3: one current rank/title/start | One summary row per person, updated in the same transaction as each new duty |
| 4: the current duty has no end date | At most one open duty per person, also enforced by a unique partial index |
| 5: the previous duty ends the day before the next starts | Start dates must strictly increase per person; the previous duty is closed in the same transaction |
| 6: RETIRED means retired | The title is canonicalized (`retired` → `RETIRED`). The API decides status in one place and reports it: `astronaut.status` (`ACTIVE` / `RETIRED`) and each duty's `isRetirement`. Clients never parse titles |
| 7: career ends the day before retirement | While retired, career end = retirement start − 1 day; it clears on reactivation |

All of these live in one pure domain operation (`CareerTransitions.AssignDuty`), which validates before changing anything. The handler runs it inside an immediate SQLite write transaction, so concurrent appends cannot interleave.

### Policies where the assignment is silent

- **Reactivation is allowed.** A retired person can take a new active duty; the current career end clears and the retirement stays in the history. Career start stays the first active date.
- **Retirement must follow an active duty.** Retirement as a first duty (`first_duty_retirement`) or while already retired (`already_retired`) is refused.
- **No future-dated duties.** "Current" means effective now; the business date is UTC (the `businessCalendar` query).
- **Dates are calendar dates** (`YYYY-MM-DD`, the `LocalDate` scalar), never time-zone converted; operation timestamps are UTC.
- **History is append-only.** The only change to an existing duty is closing its end date. Corrections or backfills would need a separate operation.
- **Invalid input is a `ValidationError`; a valid request that conflicts with the current state is a conflict error** (`ChronologyConflictError`, `ConcurrencyConflictError`, …). Rejections never partially change data.

### Ingesting the external feed

The assignment says people and duties are updated by an external service that ACTS does not control. No interface or payload for it was supplied, so the API is the ingestion boundary, and the policies above apply to the feed exactly as they apply to the UI. Some of them refuse data that a source system could legitimately send:

| The source sends | ACTS answers | Why |
| --- | --- | --- |
| A duty starting on or before the current one (late or out-of-order delivery, a backfill) | `ChronologyConflictError` (`chronology_conflict`) | History is appended in order; inserting earlier would rewrite closed duties and career dates |
| RETIRED as a person's first duty (already retired when ACTS first hears of them) | `FirstDutyRetirementError` (`first_duty_retirement`) | There is no active career for rule 7 to end |
| A duty that starts in the future (scheduled ahead) | `ValidationError` (`validation`) | "Current" means effective now |
| The same duty again (a retry) | `ChronologyConflictError`, nothing written | Makes a retry safe |
| A name two people share | `AmbiguousNameError` (`ambiguous_name`) | See rule 1 |

Nothing is refused silently. The sender gets the error type with its stable `code`, and the trace ID in the `traceresponse` header; every refusal is stored as a `Rejected` row in `ProcessLog` with the same code and trace ID (request bodies are never stored), so both sides can match it up:

```sql
-- Refused requests, newest first
SELECT datetime(OccurredAtUtc / 1000, 'unixepoch') AS At, Operation, HttpStatusCode, ErrorCode, TraceId
FROM ProcessLog WHERE Outcome = 'Rejected' ORDER BY OccurredAtUtc DESC LIMIT 50;
```

A production feed would need three things the assignment leaves open. They are product decisions for the feed's owners, so they are listed rather than guessed:
- a stable person ID from the source (and an event ID, so retries are recognized rather than refused);
- a correction operation that rebuilds a person's intervals when earlier or corrected duties arrive;
- a policy for scheduled assignments, for example holding them as pending until their start date.

### Starter defects fixed

Each was first captured as a failing test in Increment 1 and now passes.

| Starter behaviour | Now |
| --- | --- |
| `GET /AstronautDuty/{name}` returned the person, not their duties | `personByName { ... on Person { duties } }` returns the duty history, newest first |
| A name with an apostrophe caused a 500 (SQL built by string concatenation) | Matched literally; all queries are parameterized |
| The name `' OR 1=1 OR '` returned another person (SQL injection) | `NotFoundError` |
| An unknown name returned 200 | `NotFoundError` (`not_found`) |
| A duty for a missing person threw an unhandled exception | `NotFoundError` |
| Another person's identical title and date blocked a duty | Checks are per person |
| A first duty of RETIRED was accepted, with career end = start | `FirstDutyRetirementError` |
| An earlier start date was accepted, inverting the history | `ChronologyConflictError`, nothing changed |
| A repeated start date threw an unhandled exception | `ChronologyConflictError` |
| The duplicate-name check never ran (its preprocessor was not registered) | Namesakes are supported deliberately; ambiguity is refused |
| Model/migration drift stopped database creation on EF Core 9+; vulnerable transitive packages; 6 nullable warnings | Fixed; `dotnet list package --vulnerable` is clean; 0 build warnings |

## API

The business API is one GraphQL endpoint, `POST /graphql` (Hot Chocolate 16). The sign-in routes under `/auth` stay plain HTTP: issuing and revoking a session cookie is HTTP plumbing, not business data. The schema is committed as [`src/StargateAPI/schema.graphql`](src/StargateAPI/schema.graphql), with a description on every type and field; Nitro (Development) shows the same. `SchemaTests` fails if the served schema and the committed file differ. After an intended change, re-export it and review the diff like any contract change:

```bash
dotnet run --project src/StargateAPI -- schema export --output schema.graphql   # writes src/StargateAPI/schema.graphql
cd web && npm run codegen                                                      # regenerates the UI's typed operations
```

IDs are `UUID` strings and dates are `LocalDate` (`YYYY-MM-DD`). Every astronaut summary carries `status` (`ACTIVE` or `RETIRED`; no summary means no assignments) and every duty carries `isRetirement`.

| Field | Policy | Purpose |
| --- | --- | --- |
| `people(page, pageSize, name)` | People.Read | Paged directory (1-based page, size 1–100, default 25), in directory order; `name` is a case-insensitive prefix. Offset paging, because the UI pages by number in the URL |
| `person(id)` | People.Read | One person, or `null`. Selecting `duties` reads the person and history as one database snapshot |
| `personByName(name)` | People.Read | One person by exact name, as a result union: `Person`, `NotFoundError`, `AmbiguousNameError` or `ValidationError` |
| `businessCalendar` | People.Read | Business date and time zone (UTC) |
| `Person.duties` | (as its parent) | Duty history, newest first. Loaded through a DataLoader: a page of people that asks for duties costs one query, not one per person |
| `createPerson(input: {name, firstName?, middleName?, lastName?})` | People.Write | Create (namesakes allowed) |
| `updatePerson(input: {id, name, …parts, version})` | People.Write | Replace names. `version` is required by the schema; a stale one is a `ConcurrencyConflictError` |
| `upsertPersonByName(input: {lookupName, name, expectedVersion?})` | People.Write | The assignment's add/update by name; the payload's `created` says which happened. A rename needs `expectedVersion` (`VersionRequiredError`) |
| `assignDuty(input: {person: {id} or {name}, rank, dutyTitle, dutyStartDate, expectedVersion?})` | Duties.Write | Add a duty to a person chosen by ID or exact name (a `@oneOf` input: exactly one). The payload returns the new `duty` and the `person` with their new version |

**Errors are in two places**, which is the GraphQL convention:

- **Business errors are data.** Each mutation's payload has an `errors` list whose possible types the schema names per mutation (`AssignDutyError = ValidationError | NotFoundError | AmbiguousNameError | ConcurrencyConflictError | ChronologyConflictError | FirstDutyRetirementError | AlreadyRetiredError`). Every one implements `interface Error { message, code }`, and `ValidationError` adds `fields { field, messages }`. When `errors` is set, nothing was written. Branch on `__typename` or `code`, never on the message. The HTTP status is 200, and the trace ID is in the `traceresponse` header.
- **Request errors** are in the response's top-level `errors`, each with `extensions.code` and `extensions.traceId`: a field the role may not use (`forbidden`), an invalid page size (`validation`, with `extensions.fields`), the database busy (`service_unavailable`, nothing written, retry later), or a fault (`internal_error`; for a write, the outcome is unknown). Documents that do not match the schema (an unknown field, a malformed date, a missing required argument) never run. Hot Chocolate rejects them, usually with HTTP 400.

```graphql
mutation {
  assignDuty(input: { person: { name: "Grace Okonkwo" }, rank: "Captain", dutyTitle: "Instructor", dutyStartDate: "2024-06-01" }) {
    duty { id dutyStartDate }
    person { version astronaut { currentDutyTitle status } }
    errors { __typename ... on Error { code message } }
  }
}
```

What changed from the REST contract on `main`:

- **Not carried over:** the streamed "every person" list (`GET /Person`). GraphQL clients page through `people` instead; a screen, or a feed reconciliation, should never load everyone at once. The starter's date-time form for duty start dates (`2024-03-10T00:00:00`) is also gone: `LocalDate` accepts calendar dates only.
- **Moved into the schema:** unknown input fields, wrong types and missing required values (such as `updatePerson`'s version, a 428 on `main`) are rejected by GraphQL validation before anything runs.
- **Hardening:** one operation per POST (GET, batching and multipart off), a depth limit of 8 and Hot Chocolate's cost analysis, a 32 KiB body limit, exception details never returned, and introspection and Nitro in Development only.
- **One DbContext per request.** GraphQL resolves sibling fields concurrently, and they share the request's DbContext. Hot Chocolate's default is a DI scope per query resolver, which would also give each resolver its own copy of the per-request logging context. Here every resolver uses the request scope, and `OneRequestAtATimeBehavior` (a MediatR pipeline step) lets one use case at a time touch the DbContext. DataLoader batching still works.

## Sign-in and permissions

Every request requires a signed-in account except the three sign-in routes. The `/graphql` endpoint needs a session to be reached at all; each root field then checks its own policy (`@authorize` in the schema).

| Account | Role | Can |
| --- | --- | --- |
| `admin` | Admin | read people and duties, create and update people, add duties |
| `reader` | Reader | read people and duties |

- Sign-in uses an HttpOnly, Secure, SameSite=Strict cookie backed by a server-side session (8 hours, not extended by activity). The session is checked on every request, so **logout revokes it immediately**: a copied cookie stops working on its next use. Disabling an account or changing its security stamp also ends its sessions.
- Every POST needs an antiforgery token: call `GET /auth/antiforgery`, then send the `XSRF-TOKEN` cookie value as the `X-XSRF-TOKEN` header, on login, logout and every GraphQL operation (reads included, since they are POSTs too). A missing or invalid token gets 400 `csrf_failed`. JSON POSTs and SameSite=Strict cookies already block cross-site requests; the token is defence in depth, kept from the REST design.
- Not signed in is 401 `unauthenticated` (ProblemDetails, or no body for a client that does not accept JSON); there are no login redirects. A field the role may not use is a GraphQL error with code `forbidden`, and nothing runs. Five failed sign-ins lock an account for five minutes, and every failure returns the same generic 401.
- `GET /auth/me` returns the account, its roles and its capabilities (`People.Read`, `People.Write`, `Duties.Write`).
- The UI hides what a role cannot do and guards its routes, but only for usability: the API enforces every permission.

Signing in with curl (`-k` accepts the local development certificate):

```bash
curl -k -c jar -b jar https://localhost:7204/auth/antiforgery
XSRF=$(awk '/XSRF-TOKEN/ {print $7}' jar)
curl -k -c jar -b jar -H "X-XSRF-TOKEN: $XSRF" -H "Content-Type: application/json" \
     -d '{"userName":"admin","password":"Admin!2026"}' https://localhost:7204/auth/login
curl -k -b jar -H "X-XSRF-TOKEN: $XSRF" -H "Content-Type: application/json" \
     -d '{"query":"{ people(pageSize: 10) { totalCount items { id name astronaut { status } } } }"}' \
     https://localhost:7204/graphql
```

## Process logs

Successes and exceptions are stored in the `ProcessLog` table:

- **MutationAudit** rows are written by each change (create, rename, duty assignment) in the same transaction as the change itself. If the audit cannot be saved, the change rolls back.
- **RequestOutcome** rows record one outcome per HTTP request (Succeeded / Rejected / Failed / Cancelled), with status, stable error code, duration, the signed-in account's ID and, for failures, the exception type and stack trace. Exception messages, request bodies and personal names are never stored. The Development IDE (GETs of `/graphql`) is not logged.
- A GraphQL request is named by its operation type and root fields, such as `mutation assignDuty` or `query person+businessCalendar`. Those are schema names, never the client's aliases, operation name or arguments, which may contain personal names. A request that fails before it runs (invalid JSON, an unknown field) is logged as `graphql`.
- GraphQL answers 200 even when it refuses. A business error returned as data is logged as `Rejected` with its code (for example `chronology_conflict`), a denied field as `Rejected`/`forbidden`, and a resolver fault as `Failed`.
- Both rows for a request share `OperationId` and `TraceId`. `TraceId` is the value of every response's `traceresponse` header (and of `traceId` in ProblemDetails and GraphQL errors), so a reported error can be found directly. `OccurredAtUtc` is stored as Unix milliseconds.
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
# Coverage, excluding generated code (migrations, Hot Chocolate's source-generated files);
# fails the run below 50% line coverage:
dotnet test --coverlet --coverlet-output-format cobertura \
  --coverlet-exclude-by-file "**/Migrations/*.cs" --coverlet-exclude-by-file "**/obj/**" \
  --coverlet-threshold 50 --coverlet-threshold-type line --results-directory TestResults/coverage
```

Measured on 2026-10-21: **162 tests pass; line coverage 92.3% (1632/1768), branch coverage 78.2% (402/514)**. By layer: Domain 98.8%, Application 95.6%, API 89.7%, Infrastructure 85.7%. This covers all handwritten API code, including the GraphQL resolvers, authentication, sessions and logging; only generated code is excluded, by the file filters above. The rule and transition code is at 90–100%. The lowest file is the demo-account seeder: its command-line path is verified manually.

Coverage is evidence, not the goal. Every guard test was shown to fail by temporarily breaking what it protects. The most important tests:

- **`CareerTransitions.AssignDuty`** (`CareerTransitionsTests`): every rule, including retirement, reactivation, leap days and year boundaries. A rejected duty changes nothing.
- **`AssignDutyHandler`** (`DutyAssignmentApiTests`, concurrency tests): the transaction and stale-version checks. Concurrent identical or out-of-order appends leave one open duty and a contiguous history.
- **`PersonLookup`** and the name operations (`PersonReadTests`, `PersonWriteApiTests`): an ambiguous name never selects a person or writes anything; a retried rename never creates a person.
- **Authentication** (`AuthTests`): every root field has an `@authorize` policy and every mutation a write policy; Reader is denied all five write operations with nothing written; a replayed cookie after logout is rejected; CSRF is required, on GraphQL reads too.
- **The schema** (`SchemaTests`): the committed `schema.graphql` matches what the server serves.
- **GraphQL reads** (`PeopleListApiTests`, `PersonReadTests`): a page of people with their duties costs one duties query (the DataLoader), and a person with duties is read as one statement.
- **Process logging** (`ProcessLoggingTests`): if the audit cannot be saved, the change rolls back; a refusal returned as data is logged as `Rejected` with its code; operations are named by root field, never by alias or argument; a logging outage never fails a request.
- **`NameNormalizer`**, **`GraphQLErrorFilter`**, **`ApiExceptionHandler`**: matching correctness; no exception details leak to clients.
- **UI hosting** (`SpaHostingTests`): deep links reach the app shell without a session, API paths keep their own responses, and page loads are not logged.
- **Layering** (`ArchitectureTests`) and **`SqliteWriteTransactions`** (`SqliteTransactionBehaviorTests`): the dependency rule holds; the write transaction the handlers rely on takes the database lock immediately.

Test isolation:

- Domain rules (`tests/.../Domain`) are tested directly, without HTTP or a database.
- API tests run the real application against an isolated in-memory SQLite database per test. Concurrency tests use a temporary file database instead, because in-memory SQLite locks differently.
- Temporary database files live under `%TEMP%/stargate-tests/<run>` and are deleted when the run ends. Folders left by interrupted runs are removed by the next run. Tests never touch `starbase.db`.

The UI has 119 unit tests (`npm test`: stores and dialogs against Apollo's testing backend with the app's cache, error classification, guards, date formatting and serialization across time zones, timeline, return-URL safety, and the admin dialogs' validation, server-error handling, stale-version and unknown-outcome behaviour). It also has 25 browser tests (`npm run e2e`), covering:
- the introduction page;
- sign-in and session revocation;
- search and paging;
- person detail states;
- admin create, duty, retirement and rename flows, and a stale-version conflict;
- the Reader experience;
- accessibility, including the dialogs, and 200% zoom.

The browser tests create uniquely named people (`E2E Admin …`) on each run, as `npm run demo:api` does. See [Run the web UI](#run-the-web-ui).

## Limitations and deferred scope

**Security (local demo):**
- The demo passwords are public and work only in Development.
- There is no sign-in rate limiting beyond account lockout, and no timing equalization between unknown accounts and wrong passwords.
- HTTPS uses the local development certificate.

**Retrying writes:**
- Nothing is retried automatically, and there are no idempotency keys.
- Retrying a duty that may have succeeded is safe: the repeated start date is a `ChronologyConflictError` and nothing is written. For stricter guarantees, send `expectedVersion`.
- Retrying a create after a timeout can create a namesake, because duplicate names are allowed. Search by name before retrying.

**Data:**
- The migrations support fresh databases only (see above).
- The process log has no retention.
- Request bodies are limited to 32 KiB (the sign-in routes and `/graphql`).

**Not built:**
- Dark mode.
- Converting a populated database.
- Integration with the external data source the assignment mentions (no interface for it was supplied; see [Ingesting the external feed](#ingesting-the-external-feed)).

**Known untested paths:**
- The Cancelled and escaped-exception branches of request-outcome logging.
- A rename racing a duty append. Both take the same write lock, so they serialize by construction.
- The 32 KiB limit on `/graphql`: it is set per request on the server's body-size feature, which the in-process test server does not provide.
- Lock contention under load.

**GraphQL scope:**
- No subscriptions (live updates) and no persisted operations; both are natural next steps with Hot Chocolate.
- No Relay global object identification (`node(id:)`): IDs are the domain's UUIDs.
- The UI uses Apollo's cache only as a by-product: reads are network-only, because the stores own their state.

Big Picture Summary, end to end: https://claude.ai/artifact/WNs87S4h94CTSLAxSEuA8o#821edae3-7507.mrt48mjjmn9.31384
