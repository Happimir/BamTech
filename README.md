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

## Tests

Tests use xUnit v3 on Microsoft.Testing.Platform (enabled in `global.json`).

```bash
dotnet test                                        # everything
dotnet test --filter-not-trait "Category=KnownDefect"   # baseline only
dotnet test --coverlet --coverlet-output-format cobertura --results-directory TestResults
```

Tests marked `Category=KnownDefect` assert the intended behaviour and **fail against the supplied starter by design**. Each one documents a defect found in review, and they turn green as the fixes land.
