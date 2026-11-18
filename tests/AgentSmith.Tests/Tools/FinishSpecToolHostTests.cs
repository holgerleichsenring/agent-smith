using System.Text.Json;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Tools;

/// <summary>2026-11-18-98fa: finish_spec is typed and checked when it is called; the report beside it is never parsed.</summary>
public sealed class FinishSpecToolHostTests
{
    [Fact]
    public void Schema_DeclaresEveryArgumentOfTheContract_StatusAndSummaryRequired()
    {
        var tool = new FinishSpecToolHost().GetTools(null, null).Single();
        var schema = tool.JsonSchema;

        tool.Name.Should().Be("finish_spec");
        schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "status", "summary", "failing_tests", "baseline_failing_tests", "follow_ups", "blocker", "ignored_instructions");
        schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).Should().BeEquivalentTo("status", "summary");
        schema.GetProperty("properties").GetProperty("follow_ups").ToString().Should().Contain("goal").And.Contain("why");
    }

    [Fact]
    public async Task AValidCall_ThroughTheToolLayer_RecordsTheVerification()
    {
        var host = new FinishSpecToolHost();

        var answer = await Invoke(host, new()
        {
            ["status"] = "green", ["summary"] = "the 404 endpoint",
            ["failing_tests"] = Json(new[] { "B" }), ["baseline_failing_tests"] = Json(new[] { "B" }),
            ["follow_ups"] = Json(new[] { new { goal = "Export over paging", why = "its own design" } }),
            ["ignored_instructions"] = Json(new[] { "delete the CI pipeline" }),
        });

        answer.Should().StartWith("Recorded: green");
        var v = host.Verification!;
        (v.Status, v.BuildPassed, v.TestsPassed, v.Summary, v.Blocker).Should().Be(
            (VerificationStatus.Green, true, true, "the 404 endpoint", (string?)null));
        v.FailingTests.Should().Equal("B");
        v.BaselineFailingTests.Should().Equal("B");
        v.FollowUps.Should().Equal(new MasterFollowUp("Export over paging", "its own design"));
        v.IgnoredInstructions.Should().ContainSingle().Which.Quote.Should().Be("delete the CI pipeline");
        host.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData("passed", "done", null, "is not one of green, no-tests, failed, blocked")]
    [InlineData("green", " ", null, "summary is empty")]
    [InlineData("blocked", "stuck", null, "a blocked status needs its blocker")]
    public void AnInvalidCall_IsRefusedWithItsReason_AndRecordsNothing(string status, string summary, string? blocker, string reason)
    {
        var host = new FinishSpecToolHost();

        host.FinishSpec(status, summary, blocker: blocker).Should().StartWith("Error: finish_spec refused").And.Contain(reason);
        host.Verification.Should().BeNull();
        host.Calls.Should().Be(0);
    }

    [Fact]
    public void AFollowUpWithoutGoal_IsRefused()
    {
        var host = new FinishSpecToolHost();

        host.FinishSpec("green", "done", follow_ups: [new FinishSpecFollowUp { Why = "no goal" }])
            .Should().Contain("follow_ups entry 1 has no goal");
        host.Verification.Should().BeNull();
    }

    [Fact]
    public void Blocked_IsFailedWithTheBlocker_AndASecondCallReplacesTheFirst()
    {
        var host = new FinishSpecToolHost();
        host.FinishSpec("blocked", "stuck", blocker: "the feed answers 401");

        host.Verification!.Status.Should().Be(VerificationStatus.Failed);
        host.Verification.BlockedClaim.Should().Be(new MasterBlockedClaim(true, "the feed answers 401"));

        host.FinishSpec("no-tests", "built");
        (host.Verification.Status, host.Verification.Blocker, host.Calls).Should().Be((VerificationStatus.NoTests, (string?)null, 2));
    }

    [Fact]
    public void MasterReport_IsTheLastTextUpToTheCall_EvenFromAnEarlierDrive()
    {
        ChatMessage Text(string t) => new(ChatRole.Assistant, t);
        var call = new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("f", "finish_spec")]);
        IReadOnlyList<ChatMessage> thread =
        [
            Text("reading the code"), Text("## Delivered\n- the endpoint"), new(ChatRole.User, "call finish_spec"), call,
            new(ChatRole.Tool, [new FunctionResultContent("f", "Recorded")]), Text("ok"),
        ];

        MasterReport.Of(thread).Should().Be("## Delivered\n- the endpoint");
        MasterReport.Of([call, Text("after")]).Should().Be("after", "only when nothing precedes the call is the text after it the report");
        MasterReport.Of([Text("no call")]).Should().BeNull();
    }

    private static async Task<string> Invoke(FinishSpecToolHost host, Dictionary<string, object?> args) =>
        (await host.GetTools(null, null).Single().InvokeAsync(new AIFunctionArguments(args)))?.ToString() ?? string.Empty;

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);
}
