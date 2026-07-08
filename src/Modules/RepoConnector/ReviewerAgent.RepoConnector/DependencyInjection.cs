using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReviewerAgent.RepoConnector.Contracts;

namespace ReviewerAgent.RepoConnector;

/// <summary>Registers the RepoConnector module.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddRepoConnectorModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RepoConnectorOptions>(configuration.GetSection(RepoConnectorOptions.SectionName));

        services.AddHttpClient<AzureDevOpsRepoConnector>(ConfigureAzureDevOpsClient);
        services.AddHttpClient<GitHubRepoConnector>(ConfigureGitHubClient);
        services.AddHttpClient<GitLabRepoConnector>(ConfigureGitLabClient);

        services.AddSingleton<IRepoConnectorFactory, RepoConnectorFactory>();
        services.AddTransient<IRepoConnector>(sp => sp.GetRequiredService<IRepoConnectorFactory>().Create());
        return services;
    }

    private static void ConfigureAzureDevOpsClient(IServiceProvider sp, HttpClient client)
    {
        var o = sp.GetRequiredService<IOptions<RepoConnectorOptions>>().Value;
        client.BaseAddress = new Uri(
            $"{o.BaseUrl.TrimEnd('/')}/{o.Organization}/{o.Project}/_apis/git/repositories/{o.Repository}/");
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{o.Pat}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private static void ConfigureGitHubClient(IServiceProvider sp, HttpClient client)
    {
        var o = sp.GetRequiredService<IOptions<RepoConnectorOptions>>().Value;
        client.BaseAddress = new Uri($"{o.BaseUrl.TrimEnd('/')}/repos/{o.Organization}/{o.Repository}/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", o.Pat);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        // GitHub rejects requests without a User-Agent.
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ReviewerAgent", "1.0"));
    }

    private static void ConfigureGitLabClient(IServiceProvider sp, HttpClient client)
    {
        var o = sp.GetRequiredService<IOptions<RepoConnectorOptions>>().Value;
        // GitLab addresses a project by numeric id or URL-encoded path; either is escaped here.
        client.BaseAddress = new Uri($"{o.BaseUrl.TrimEnd('/')}/projects/{Uri.EscapeDataString(o.Repository)}/");
        client.DefaultRequestHeaders.TryAddWithoutValidation("PRIVATE-TOKEN", o.Pat);
    }
}
