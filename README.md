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
