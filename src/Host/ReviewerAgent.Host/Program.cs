using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReviewerAgent.CodeScanner;
using ReviewerAgent.CommentGenerator;
using ReviewerAgent.ContextBuilder;
using ReviewerAgent.Host;
using ReviewerAgent.LlmConnector;
using ReviewerAgent.PromptManager;
using ReviewerAgent.RepoConnector;
using ReviewerAgent.Reviewer;
using ReviewerAgent.Rules;
using ReviewerAgent.TaskConnector;
using ReviewerAgent.TaskManager;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;

// Configuration sources are already wired by Host.CreateApplicationBuilder in this order
// (later wins): appsettings.json -> appsettings.{Environment}.json -> User Secrets
// (Development only) -> environment variables -> command-line args. Any variable named with
// the "__" separator binds natively, e.g. LlmConnector__ApiKey -> config["LlmConnector:ApiKey"].
//
// The overrides below only add SHORT, provider-conventional aliases (CLAUDE_API_KEY,
// AZURE_DEVOPS_PAT, ...) on top of that native mapping. Each alias, when set, overrides one or
// more configuration keys; when unset, the value already bound from the sources above is kept.

// Azure DevOps (repository + boards).
ApplyEnvOverride("AZURE_DEVOPS_BASE_URL", "RepoConnector:BaseUrl", "TaskConnector:BaseUrl");
ApplyEnvOverride("AZURE_DEVOPS_ORG", "RepoConnector:Organization", "TaskConnector:Organization");
ApplyEnvOverride("AZURE_DEVOPS_PROJECT", "RepoConnector:Project", "TaskConnector:Project");
ApplyEnvOverride("AZURE_DEVOPS_REPOSITORY", "RepoConnector:Repository");
ApplyEnvOverride("AZURE_DEVOPS_PAT", "RepoConnector:Pat", "TaskConnector:Pat");

// GitHub / GitLab (repository host token maps to the same RepoConnector:Pat slot).
ApplyEnvOverride("GITHUB_TOKEN", "RepoConnector:Pat");
ApplyEnvOverride("GITLAB_TOKEN", "RepoConnector:Pat");

// ClickUp.
ApplyEnvOverride("CLICKUP_BASE_URL", "ClickUp:BaseUrl");
ApplyEnvOverride("CLICKUP_API_TOKEN", "ClickUp:ApiToken");
ApplyEnvOverride("CLICKUP_TEAM_ID", "ClickUp:TeamId");
ApplyEnvOverride("CLICKUP_SPACE_ID", "ClickUp:SpaceId");
ApplyEnvOverride("CLICKUP_LIST_ID", "ClickUp:ListId");

// Jira.
ApplyEnvOverride("JIRA_BASE_URL", "Jira:BaseUrl");
ApplyEnvOverride("JIRA_EMAIL", "Jira:Email");
ApplyEnvOverride("JIRA_API_TOKEN", "Jira:ApiToken");

// Claude API (LLM connector). CLAUDE_API_KEY is preferred; LLM_API_KEY kept for compatibility.
ApplyEnvOverride("CLAUDE_API_KEY", "LlmConnector:ApiKey");
ApplyEnvOverride("LLM_API_KEY", "LlmConnector:ApiKey");
// OAuth fallback (Claude subscription): used only when no API key is configured.
ApplyEnvOverride("ANTHROPIC_AUTH_TOKEN", "LlmConnector:AuthToken");
ApplyEnvOverride("CLAUDE_CODE_OAUTH_TOKEN", "LlmConnector:AuthToken");
ApplyEnvOverride("CLAUDE_MODEL", "LlmConnector:Model");
ApplyEnvOverride("CLAUDE_BASE_URL", "LlmConnector:BaseUrl");

// Fail fast on missing PAT (validates wiring without hitting Azure DevOps).
if (string.IsNullOrWhiteSpace(config["RepoConnector:Pat"]))
{
    Console.Error.WriteLine("Azure DevOps PAT is not configured. Set the AZURE_DEVOPS_PAT environment variable or RepoConnector:Pat.");
    return 1;
}

var prId = ResolvePullRequestId(args, config);
if (prId <= 0)
{
    Console.Error.WriteLine("No pull request id provided. Pass it as the first argument, set REVIEW_PR_ID, or configure Review:PrId.");
    return 1;
}

builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Extensions.Hosting", LogLevel.Warning);


var seqServerUrl = Environment.GetEnvironmentVariable("SEQ_SERVER_URL") ?? config["Seq:ServerUrl"];
if (!string.IsNullOrWhiteSpace(seqServerUrl))
{
    var seqApiKey = Environment.GetEnvironmentVariable("SEQ_API_KEY") ?? config["Seq:ApiKey"];
    builder.Logging.AddSeq(seqServerUrl, apiKey: string.IsNullOrWhiteSpace(seqApiKey) ? null : seqApiKey);
}

builder.Services
    .AddRepoConnectorModule(config)
    .AddTaskConnectorModule(config)
    .AddLlmConnectorModule(config)
    .AddCodeScannerModule(config)
    .AddTaskManagerModule(config)
    .AddContextBuilderModule(config)
    .AddRulesModule(config)
    .AddPromptManagerModule(config)
    .AddReviewerModule(config)
    .AddCommentGeneratorModule(config);

builder.Services.AddSingleton(new ReviewRequest(prId));
builder.Services.AddHostedService<ReviewWorker>();

var app = builder.Build();
await app.RunAsync();
return 0;

// Reads an environment variable and, when present, writes it to the given configuration
// key(s). When the variable is unset/blank the existing appsettings.json value is kept.
void ApplyEnvOverride(string envName, params string[] configKeys)
{
    var value = Environment.GetEnvironmentVariable(envName);
    if (string.IsNullOrWhiteSpace(value))
    {
        return;
    }

    foreach (var key in configKeys)
    {
        config[key] = value;
    }
}

static int ResolvePullRequestId(string[] args, IConfiguration config)
{
    if (args.Length > 0 && int.TryParse(args[0], out var fromArg))
    {
        return fromArg;
    }

    var fromEnv = Environment.GetEnvironmentVariable("REVIEW_PR_ID");
    if (int.TryParse(fromEnv, out var envId))
    {
        return envId;
    }

    return int.TryParse(config["Review:PrId"], out var cfgId) ? cfgId : 0;
}
