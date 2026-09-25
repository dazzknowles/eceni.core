# Eceni.Core

Shared .NET libraries for Eceni projects, published as private NuGet packages via GitHub Packages.

## Packages

- **Eceni.Core.Base** — data-access foundation: `IDBUtility`/`DBUtility` (static, multi-provider facade), the
  explicit local `IDbUnitOfWork` unit-of-work abstraction, `BaseEntity`/`BaseRepository`, `DatabaseConfig`/
  `DBConnectionResolver`, and the reflection-based mapping helpers in `DBUtilityCommon`.
- **Eceni.Core.Database.MySQL** — the MySQL/MariaDB `IDBUtility` provider (`DBUtilityMySQL`), built on
  [MySqlConnector](https://mysqlconnector.net/), plus `AddEceniCoreDatabaseMySQL()` for DI registration.
- **Eceni.Core.Secrets** — provider-neutral `IEceniSecrets` and `IEceniVariables` access, configuration-based
  provider selection, in-memory caching, required-value exceptions/startup validation, and in-memory test secrets.
- **Eceni.Core.Secrets.Infisical** — local-development secrets from Infisical using Universal Auth.
- **Eceni.Core.Secrets.Aws** — production secrets from AWS Secrets Manager using the normal AWS credential chain.

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

## Secrets and variables

Install `Eceni.Core.Secrets` and each provider the deployed application can select, then register them once in the
composition root:

```csharp
using Eceni.Core.Secrets.Aws.Extensions;
using Eceni.Core.Secrets.Extensions;
using Eceni.Core.Secrets.Infisical.Extensions;

builder.Services
    .AddEceniSecrets(builder.Configuration)
    .AddInfisicalProvider()
    .AddAwsSecretsManagerProvider();
```

Application code depends only on the provider-neutral interfaces:

```csharp
public sealed class PaymentService(IEceniSecrets secrets)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        string apiKey = await secrets.GetRequiredSecretAsync("STRIPE_API_KEY", cancellationToken);
        // Use apiKey without knowing which provider supplied it.
    }
}
```

Local Infisical configuration:

```json
{
  "Eceni": {
    "Secrets": {
      "Provider": "Infisical",
      "CacheDuration": "00:05:00",
      "MissingCacheDuration": "00:00:30",
      "Infisical": {
        "ProjectId": "project-id",
        "Environment": "dev",
        "SecretPath": "/eceni"
      }
    },
    "Variables": {
      "Provider": "Configuration",
      "Section": "Variables"
    }
  }
}
```

Set `INFISICAL_CLIENT_ID` and `INFISICAL_CLIENT_SECRET` outside committed configuration. Their environment-variable
names can be changed with `ClientIdEnvironmentVariable` and `ClientSecretEnvironmentVariable` in the Infisical
section.

Production AWS configuration:

```json
{
  "Eceni": {
    "Secrets": {
      "Provider": "AwsSecretsManager",
      "Prefix": "eceni/production/",
      "CacheDuration": "00:15:00",
      "Required": [ "DATABASE_PASSWORD", "STRIPE_API_KEY" ],
      "ValidateOnStart": true,
      "AwsSecretsManager": {
        "Region": "eu-west-2"
      },
      "Mappings": {
        "STRIPE_API_KEY": "shared/payments/stripe-api-key"
      }
    }
  }
}
```

AWS credentials are resolved by the AWS SDK, so production should use the workload's IAM role. `Prefix` applies to
unmapped logical names; an entry in `Mappings` is the complete provider-side name and takes precedence.

`GetSecretAsync()` returns null for a missing value and `GetRequiredSecretAsync()` throws
`SecretNotFoundException`. Provider failures are not cached. Concurrent requests for the same uncached secret are
coalesced into one provider call. Binary AWS secrets are returned as Base64 strings.

Variables default to the configured `Variables` section. `GetVariable<T>()` supports normal invariant string
conversion, while structured configuration should generally continue to use `IOptions<T>`.

Tests can select the `InMemory` provider without either external SDK:

```csharp
Dictionary<string, string> testSecrets = new()
{
    ["STRIPE_API_KEY"] = "test-key"
};

services.AddEceniSecrets(configuration).AddInMemorySecretProvider(testSecrets);
```

## Consuming the packages

Packages publish to GitHub Packages on a `v*.*.*` tag push. To restore them from another project, add a
`NuGet.config` pointing at `https://nuget.pkg.github.com/dazzknowles/index.json` with a GitHub personal access
token that has `read:packages` scope.
