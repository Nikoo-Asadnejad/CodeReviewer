using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReviewerAgent.TaskConnector.Contracts;

namespace ReviewerAgent.TaskConnector;

/// <summary>Registers the TaskConnector module.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddTaskConnectorModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TaskConnectorOptions>(configuration.GetSection(TaskConnectorOptions.SectionName));
        services.Configure<ClickUpOptions>(configuration.GetSection(ClickUpOptions.SectionName));
        services.Configure<JiraOptions>(configuration.GetSection(JiraOptions.SectionName));

        services.AddHttpClient<AzureDevOpsTaskConnector>(ConfigureAzureDevOpsClient);
        services.AddHttpClient<ClickUpTaskConnector>(ConfigureClickUpClient);
        services.AddHttpClient<JiraTaskConnector>(ConfigureJiraClient);

        services.AddSingleton<ITaskConnectorFactory, TaskConnectorFactory>();
        services.AddTransient<ITaskConnector>(sp => sp.GetRequiredService<ITaskConnectorFactory>().Create());
        return services;
    }

    private static void ConfigureAzureDevOpsClient(IServiceProvider sp, HttpClient client)
    {
        var o = sp.GetRequiredService<IOptions<TaskConnectorOptions>>().Value;
        client.BaseAddress = new Uri($"{o.BaseUrl.TrimEnd('/')}/{o.Organization}/{o.Project}/_apis/wit/");
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{o.Pat}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private static void ConfigureClickUpClient(IServiceProvider sp, HttpClient client)
    {
        var o = sp.GetRequiredService<IOptions<ClickUpOptions>>().Value;
        client.BaseAddress = new Uri($"{o.BaseUrl.TrimEnd('/')}/");
        // ClickUp personal tokens are sent verbatim in the Authorization header (no scheme).
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", o.ApiToken);
    }

    private static void ConfigureJiraClient(IServiceProvider sp, HttpClient client)
    {
        var o = sp.GetRequiredService<IOptions<JiraOptions>>().Value;
        client.BaseAddress = new Uri($"{o.BaseUrl.TrimEnd('/')}/rest/api/3/");
        // Jira Cloud uses Basic auth with email:apiToken.
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{o.Email}:{o.ApiToken}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }
}
