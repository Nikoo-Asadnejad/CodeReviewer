using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ReviewerAgent.SharedKernel;
using ReviewerAgent.TaskConnector;
using Xunit;

namespace ReviewerAgent.Tests;

public class TaskConnectorTests
{
    private static AzureDevOpsTaskConnector Build(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var client = StubHttpMessageHandler.Client(responder, "https://dev.azure.com/org/proj/_apis/wit/", out _);
        return new AzureDevOpsTaskConnector(client, NullLogger<AzureDevOpsTaskConnector>.Instance);
    }

    [Fact]
    public async Task Maps_work_item_fields()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json(
            """
            {
              "id": 123,
              "fields": {
                "System.Title": "Add login",
                "System.Description": "Implement login flow",
                "Microsoft.VSTS.Common.AcceptanceCriteria": "User can sign in",
                "System.State": "Active"
              }
            }
            """));

        var item = await connector.GetWorkItemAsync("123");

        Assert.NotNull(item);
        Assert.Equal("123", item!.Id);
        Assert.Equal("Add login", item.Title);
        Assert.Equal("Implement login flow", item.Description);
        Assert.Equal("User can sign in", item.AcceptanceCriteria);
        Assert.Equal("Active", item.State);
    }

    [Fact]
    public async Task Returns_null_when_work_item_not_found()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json("{}", HttpStatusCode.NotFound));

        Assert.Null(await connector.GetWorkItemAsync("999"));
    }

    [Fact]
    public async Task Throws_on_server_error()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json("boom", HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<RepoConnectorException>(() => connector.GetWorkItemAsync("1"));
    }
}
