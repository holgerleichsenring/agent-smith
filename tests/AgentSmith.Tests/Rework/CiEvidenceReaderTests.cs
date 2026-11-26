using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>
/// 2026-11-26-49d3a: a code run on a ticket reads the red CI on its open pull requests from the records, with the stored
/// evidence, and starts with it in the prompt after the review — whoever started it; nothing else is set.
/// </summary>
public sealed class CiEvidenceReaderTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 26, 8, 0, 0, TimeSpan.Zero);
    private static readonly CiFailure Failing = new("PR build", "https://ci.example/812",
        "test failed: Shop.Integration.OrderImportTests.Imports_the_batch\nExpected 12 orders, found 11.");

    [Fact]
    public async Task ARedOpenHead_LandsInTheContextAndThePrompt()
    {
        var pipeline = await ReadAsync(Record("api", "https://github.com/o/api/pull/1", PrReviewStates.Open, "abc", CiStates.Red, "abc"));

        pipeline.Get<IReadOnlyList<CiFailureFeedback>>(ContextKeys.CiFailures).Single().Failures.Should().Equal(Failing);
        var prompt = TicketConversationPromptSection.Render(pipeline);
        prompt.Should().Contain(CiFailurePromptSection.Heading).And.Contain("### PR build (api, https://github.com/o/api/pull/1)")
            .And.Contain("Expected 12 orders, found 11.");
        pipeline.Has(ContextKeys.ReworkAct).Should().BeFalse("the evidence is data; no act is set");
    }

    [Fact]
    public async Task GreenAnOlderHeadOrAClosedPullRequest_SetNothing()
    {
        var pipeline = await ReadAsync(
            Record("api", "https://github.com/o/api/pull/1", PrReviewStates.Open, "abc", CiStates.Green, "abc"),
            Record("web", "https://github.com/o/web/pull/2", PrReviewStates.Open, "new", CiStates.Red, "old"),
            Record("ops", "https://github.com/o/ops/pull/3", PrReviewStates.Merged, "abc", CiStates.Red, "abc"));

        pipeline.Has(ContextKeys.CiFailures).Should().BeFalse();
        CiFailurePromptSection.Render(pipeline).Should().BeEmpty();
    }

    private static async Task<PipelineContext> ReadAsync(params PrReviewRecordView[] records)
    {
        var reader = new Mock<IPrReviewRecordReader>();
        reader.Setup(r => r.ByTicketAsync("alpha", "1", It.IsAny<CancellationToken>())).ReturnsAsync(records);
        using var services = new ServiceCollection().AddSingleton(reader.Object).BuildServiceProvider();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject { Name = "alpha" });
        await new CiEvidenceReader(services.GetRequiredService<IServiceScopeFactory>())
            .ApplyAsync(new FetchTicketContext(new TicketId("1"), new TrackerConnection(), pipeline), CancellationToken.None);
        return pipeline;
    }

    private static PrReviewRecordView Record(string repo, string url, string state, string head, string ci, string ciHead) =>
        new("k-" + repo, url, repo, "1", "alpha", "1", "run-1", state, T, T, null, [])
        { HeadSha = head, Ci = new PrRecordCiView(ci, ciHead, T, ci == CiStates.Red ? [Failing] : []) };
}
