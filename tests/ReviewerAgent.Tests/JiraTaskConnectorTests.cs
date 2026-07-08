using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReviewerAgent.SharedKernel;
using ReviewerAgent.TaskConnector;
using Xunit;

namespace ReviewerAgent.Tests;

public class JiraTaskConnectorTests
{
    private static JiraTaskConnector Build(
        Func<HttpRequestMessage, HttpResponseMessage> responder, JiraOptions? options = null)
    {
        var client = StubHttpMessageHandler.Client(responder, "https://org.atlassian.net/rest/api/3/", out _);
        return new JiraTaskConnector(client, Options.Create(options ?? new JiraOptions()), NullLogger<JiraTaskConnector>.Instance);
    }

    [Fact]
    public async Task Maps_summary_status_and_flattens_adf_description()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json(
            """
            {
              "key": "FLY-123",
              "fields": {
                "summary": "Add login",
                "status": { "name": "In Progress" },
                "description": {
                  "type": "doc",
                  "content": [
                    { "type": "paragraph", "content": [ { "type": "text", "text": "Implement login flow" } ] },
                    { "type": "paragraph", "content": [ { "type": "text", "text": "Second line" } ] }
                  ]
                }
              }
            }
            """));

        var item = await connector.GetWorkItemAsync("FLY-123");

        Assert.NotNull(item);
        Assert.Equal("FLY-123", item!.Id);
        Assert.Equal("Add login", item.Title);
        Assert.Equal("In Progress", item.State);
        Assert.Equal("Implement login flow\nSecond line", item.Description);
    }

    [Fact]
    public async Task Maps_acceptance_criteria_from_configured_custom_field()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json(
            """
            {
              "key": "FLY-9",
              "fields": {
                "summary": "Thing",
                "status": { "name": "To Do" },
                "description": null,
                "customfield_10001": "User can sign in"
              }
            }
            """),
            new JiraOptions { AcceptanceCriteriaField = "customfield_10001" });

        var item = await connector.GetWorkItemAsync("FLY-9");

        Assert.NotNull(item);
        Assert.Equal("User can sign in", item!.AcceptanceCriteria);
        Assert.Null(item.Description);
    }

    [Fact]
    public async Task Returns_null_when_issue_not_found()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json("{}", HttpStatusCode.NotFound));

        Assert.Null(await connector.GetWorkItemAsync("FLY-404"));
    }

    [Fact]
    public async Task Throws_on_server_error()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json("boom", HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<RepoConnectorException>(() => connector.GetWorkItemAsync("FLY-1"));
    }
}
