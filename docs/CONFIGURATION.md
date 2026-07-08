# Configuration & Secrets

This project follows the standard .NET configuration model. Settings are read through
`IConfiguration` and bound to strongly-typed `Options` classes. Sources are layered by
`Host.CreateApplicationBuilder`, and **later sources win**:

1. `appsettings.json` — non-secret defaults, committed to the repo.
2. `appsettings.{Environment}.json` — per-environment non-secret overrides (optional).
3. **User Secrets** — local development secrets. Loaded **only** when the environment is
   `Development`. Stored in your user profile, never in the repo.
4. **Environment variables** — the mechanism for Docker / CI / production secrets.
5. Command-line arguments.

> Never commit secrets to `appsettings.json`. Use User Secrets locally and environment
> variables in deployed environments.

## Local development — User Secrets (recommended)

User Secrets is the idiomatic .NET store for local secrets. It keeps them outside the repo
so they can't be accidentally committed. The host loads them automatically because the
`ReviewerAgent` launch profile sets `DOTNET_ENVIRONMENT=Development`.

Run these from the host project directory (`src/Host/ReviewerAgent.Host`):

```bash
# LLM credential (use ONE):
dotnet user-secrets set "LlmConnector:ApiKey" "sk-ant-..."
# or, for a Claude subscription OAuth token:
dotnet user-secrets set "LlmConnector:AuthToken" "..."

# Azure DevOps PAT:
dotnet user-secrets set "RepoConnector:Pat" "..."

# Inspect / clear:
dotnet user-secrets list
dotnet user-secrets remove "LlmConnector:ApiKey"
```

Then run normally (`dotnet run`) — the values are injected without touching any file in the repo.

## Environment variables (Docker / CI / production)

The built-in Environment Variables provider maps any variable to a configuration key using
`__` (double underscore) as the section separator — **no code required**:

| Environment variable          | Configuration key           |
| ----------------------------- | --------------------------- |
| `LlmConnector__ApiKey`        | `LlmConnector:ApiKey`       |
| `LlmConnector__AuthToken`     | `LlmConnector:AuthToken`    |
| `RepoConnector__Pat`          | `RepoConnector:Pat`         |

For convenience, `Program.cs` also accepts a set of **short provider-conventional aliases**
that map onto the same keys:

| Alias                     | Target configuration key(s)                 |
| ------------------------- | ------------------------------------------- |
| `CLAUDE_API_KEY`          | `LlmConnector:ApiKey`                        |
| `ANTHROPIC_AUTH_TOKEN`    | `LlmConnector:AuthToken`                     |
| `CLAUDE_MODEL`            | `LlmConnector:Model`                         |
| `CLAUDE_BASE_URL`         | `LlmConnector:BaseUrl`                       |
| `AZURE_DEVOPS_PAT`        | `RepoConnector:Pat`                          |
| `GITHUB_TOKEN` / `GITLAB_TOKEN` | `RepoConnector:Pat`                    |

(See `Program.cs` for the full alias list.)

### Docker

```bash
docker build -t revieweragent .
docker run -e CLAUDE_API_KEY=sk-ant-... -e AZURE_DEVOPS_PAT=... revieweragent
# or with a local env file that stays out of source control:
docker run --env-file secrets.env revieweragent
```

## Fail-fast validation

`LlmConnector` options are validated at **host startup** (`ValidateOnStart`). A missing
credential or invalid value throws a clear `OptionsValidationException` immediately, rather
than surfacing deep inside the HTTP client on first use.
