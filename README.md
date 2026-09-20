# Eceni.Core

Shared .NET libraries for Eceni projects, published as private NuGet packages via GitHub Packages.

## Packages

- **Eceni.Core.Base** — data-access foundation: `IDBUtility`/`DBUtility` (static, multi-provider facade), the
  explicit local `IDbUnitOfWork` unit-of-work abstraction, `BaseEntity`/`BaseRepository`, `DatabaseConfig`/
  `DBConnectionResolver`, and the reflection-based mapping helpers in `DBUtilityCommon`.
- **Eceni.Core.Database.MySQL** — the MySQL/MariaDB `IDBUtility` provider (`DBUtilityMySQL`), built on
  [MySqlConnector](https://mysqlconnector.net/), plus `AddEceniCoreDatabaseMySQL()` for DI registration.

Both projects are async-only (no `[Obsolete]` synchronous twins) and use stored procedures exclusively for
parameterized data access.

## Build

```bash
dotnet restore
dotnet build --no-restore
dotnet test tests/Eceni.Core.UnitTest --no-build
```

Unit-of-work tests that need a live MySQL/MariaDB instance are skipped (Inconclusive) unless
`ECENI_TEST_MYSQL_CONNECTION_STRING` is set — CI runs with no database service.

## Consuming the packages

Packages publish to GitHub Packages on a `v*.*.*` tag push. To restore them from another project, add a
`NuGet.config` pointing at `https://nuget.pkg.github.com/dazzknowles/index.json` with a GitHub personal access
token that has `read:packages` scope.
