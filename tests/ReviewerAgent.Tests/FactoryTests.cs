using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ReviewerAgent.LlmConnector;
using ReviewerAgent.RepoConnector;
using ReviewerAgent.SharedKernel;
using ReviewerAgent.TaskConnector;
using Xunit;

namespace ReviewerAgent.Tests;

public class FactoryTests
{
    [Fact]
    public void RepoFactory_selects_azure_devops()
    {
        var connector = new AzureDevOpsRepoConnector(new HttpClient(), NullLogger<AzureDevOpsRepoConnector>.Instance);
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(AzureDevOpsRepoConnector))).Returns(connector);

        var factory = new RepoConnectorFactory(sp.Object, Options.Create(new RepoConnectorOptions { Provider = "azuredevops" }));

        Assert.Same(connector, factory.Create());
    }

    [Fact]
    public void RepoFactory_selects_github()
    {
        var connector = new GitHubRepoConnector(new HttpClient(), NullLogger<GitHubRepoConnector>.Instance);
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(GitHubRepoConnector))).Returns(connector);

        var factory = new RepoConnectorFactory(sp.Object, Options.Create(new RepoConnectorOptions { Provider = "github" }));

        Assert.Same(connector, factory.Create());
    }

    [Fact]
    public void RepoFactory_selects_gitlab()
    {
        var connector = new GitLabRepoConnector(new HttpClient(), NullLogger<GitLabRepoConnector>.Instance);
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(GitLabRepoConnector))).Returns(connector);

        var factory = new RepoConnectorFactory(sp.Object, Options.Create(new RepoConnectorOptions { Provider = "gitlab" }));

        Assert.Same(connector, factory.Create());
    }

    [Fact]
    public void RepoFactory_throws_on_unknown_provider()
    {
        var factory = new RepoConnectorFactory(
            new Mock<IServiceProvider>().Object,
            Options.Create(new RepoConnectorOptions { Provider = "bitbucket" }));

        Assert.Throws<RepoConnectorException>(() => factory.Create());
    }

    [Fact]
    public void TaskFactory_selects_jira()
    {
        var connector = new JiraTaskConnector(
            new HttpClient(), Options.Create(new JiraOptions()), NullLogger<JiraTaskConnector>.Instance);
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(JiraTaskConnector))).Returns(connector);

        var factory = new TaskConnectorFactory(sp.Object, Options.Create(new TaskConnectorOptions { Provider = "jira" }));

        Assert.Same(connector, factory.Create());
    }

    [Fact]
    public void TaskFactory_throws_on_unknown_provider()
    {
        var factory = new TaskConnectorFactory(
            new Mock<IServiceProvider>().Object,
            Options.Create(new TaskConnectorOptions { Provider = "trello" }));

        Assert.Throws<RepoConnectorException>(() => factory.Create());
    }

    [Fact]
    public void LlmFactory_selects_anthropic()
    {
        var options = Options.Create(new LlmConnectorOptions { Provider = "anthropic" });
        var connector = new AnthropicLlmConnector(new HttpClient(), options, NullLogger<AnthropicLlmConnector>.Instance);
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(AnthropicLlmConnector))).Returns(connector);

        var factory = new LlmConnectorFactory(sp.Object, options);

        Assert.Same(connector, factory.Create());
    }

    [Fact]
    public void LlmFactory_selects_ollama()
    {
        var options = Options.Create(new LlmConnectorOptions { Provider = "ollama" });
        var connector = new OllamaLlmConnector(new HttpClient(), options, NullLogger<OllamaLlmConnector>.Instance);
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(OllamaLlmConnector))).Returns(connector);

        var factory = new LlmConnectorFactory(sp.Object, options);

        Assert.Same(connector, factory.Create());
    }

    [Fact]
    public void LlmFactory_throws_on_unknown_provider()
    {
        var factory = new LlmConnectorFactory(
            new Mock<IServiceProvider>().Object,
            Options.Create(new LlmConnectorOptions { Provider = "openai" }));

        Assert.Throws<LlmException>(() => factory.Create());
    }
}
