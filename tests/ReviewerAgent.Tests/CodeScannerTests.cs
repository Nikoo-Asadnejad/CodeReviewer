using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ReviewerAgent.CodeScanner;
using ReviewerAgent.RepoConnector.Contracts;
using Xunit;

namespace ReviewerAgent.Tests;

public class CodeScannerTests
{
    private const string Sample =
        """
        namespace Foo;
        /// <summary>A bar.</summary>
        public class Bar : IBar
        {
            public int Add(int a, int b) => a + b;
        }
        public interface IBar { }
        """;

    private static RoslynCodeScanner Scanner(IRepoConnector repo, CodeScannerOptions? options = null) =>
        new(repo, Options.Create(options ?? new CodeScannerOptions()), NullLogger<RoslynCodeScanner>.Instance);

    private static Mock<IRepoConnector> RepoWith(params ChangedFile[] files)
    {
        var repo = new Mock<IRepoConnector>();
        repo.Setup(r => r.GetPullRequestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequest(1, "t", null, "feature", "main", "me"));
        repo.Setup(r => r.GetChangedFilesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);
        return repo;
    }

    [Fact]
    public async Task Extracts_semantic_context_from_csharp()
    {
        var repo = RepoWith(new ChangedFile("Foo/Bar.cs", null, ChangeType.Modified, "", Sample));

        var result = await Scanner(repo.Object).ScanAsync(1);

        var scanned = Assert.Single(result.Files);
        Assert.Equal("Foo", scanned.Context.Namespace);
        Assert.Contains(scanned.Context.Types, t => t.Contains("Bar"));
        Assert.Contains(scanned.Context.Interfaces, i => i == "IBar");
        Assert.Contains(scanned.Context.Methods, m => m.Contains("Add"));
    }

    [Fact]
    public async Task Non_csharp_file_has_empty_context()
    {
        var repo = RepoWith(new ChangedFile("readme.txt", null, ChangeType.Added, "", "hello"));

        var result = await Scanner(repo.Object).ScanAsync(1);

        Assert.True(Assert.Single(result.Files).Context.IsEmpty);
    }

    [Fact]
    public async Task Deleted_and_ignored_files_are_excluded()
    {
        var repo = RepoWith(
            new ChangedFile("Gone.cs", null, ChangeType.Deleted, "", null),
            new ChangedFile("data.json", null, ChangeType.Added, "", "{}"),
            new ChangedFile("Keep.cs", null, ChangeType.Modified, "", Sample));

        var options = new CodeScannerOptions { IgnoredFilePatterns = [".json"] };
        var result = await Scanner(repo.Object, options).ScanAsync(1);

        Assert.Equal("Keep.cs", Assert.Single(result.Files).File.Path);
    }
}
