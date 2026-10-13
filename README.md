# BamTech — Stargate ACTS

Astronaut Career Tracking System (ACTS) technical exercise: the supplied Stargate API, enhanced, with an Angular UI to follow.

> Work in progress. The supplied starter is committed unmodified (`46b669d`), and later commits apply the fixes as reviewable diffs. Design rationale and the full API contract will be documented here before handoff.

## Prerequisites

- .NET 10 SDK (10.0.100 or later; see `global.json`)

There is no database server to install: the API uses a local SQLite file.

## Set up and run the API

```bash
dotnet tool restore                               # installs the pinned dotnet-ef locally
dotnet build                                      # restores packages; dotnet ef needs them
dotnet ef database update --project src/StargateAPI
dotnet run --project src/StargateAPI --launch-profile https
```

- The database file is `src/StargateAPI/starbase.db`. A relative `Data Source` in `ConnectionStrings:StarbaseApiDatabase` resolves against the API project folder, whichever folder you start it from.
- Swagger UI is available in Development at `https://localhost:7204/swagger`.
- **Fresh databases only.** The `ConvertToGuidIdentity` migration moves the starter's integer keys to UUIDv7 and refuses to run (making no changes) if the database already holds people or duties. Converting populated data is not implemented. Part of the migration (SQLite table rebuilds) runs outside a transaction; if it is interrupted, delete `starbase.db` and run `dotnet ef database update` again.

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
