using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReviewerAgent.Rules;
using Xunit;

namespace ReviewerAgent.Tests;

public class RulesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rules-" + Guid.NewGuid().ToString("N"));

    public RulesTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private MarkdownRuleProvider Provider(string folder) =>
        new(Options.Create(new RulesOptions { Folder = folder }), NullLogger<MarkdownRuleProvider>.Instance);

    [Fact]
    public async Task Loads_all_markdown_files()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir, "Security.md"), "sec");
        await File.WriteAllTextAsync(Path.Combine(_dir, "Naming.md"), "name");

        var rules = await Provider(_dir).LoadRulesAsync();

        Assert.Equal(2, rules.Count);
        Assert.Contains(rules, r => r.Name == "Security" && r.Content == "sec");
        Assert.Contains(rules, r => r.Name == "Naming");
    }

    [Fact]
    public async Task Empty_folder_returns_empty_list()
    {
        var rules = await Provider(_dir).LoadRulesAsync();
        Assert.Empty(rules);
    }

    [Fact]
    public async Task Missing_folder_throws()
    {
        var provider = Provider(Path.Combine(_dir, "does-not-exist"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.LoadRulesAsync());
    }
}
