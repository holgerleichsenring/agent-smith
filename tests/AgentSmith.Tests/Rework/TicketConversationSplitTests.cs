using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-7c0e: the conversation leads with what was written since the previous
/// attempt started, both parts sharing one budget.</summary>
public sealed class TicketConversationSplitTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);

    private static PipelineContext Pipeline(PreviousAttempt? attempt, params TicketComment[] comments)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketComments, (IReadOnlyList<TicketComment>)comments);
        if (attempt is not null) pipeline.Set(ContextKeys.PreviousAttempt, attempt);
        return pipeline;
    }

    private static TicketComment Said(string who, int minutes, string body) => new(who, Start.AddMinutes(minutes), body);

    [Fact]
    public void Conversation_PreviousAttempt_LeadsWithForeignCommentsAfterStart()
    {
        var attempt = new PreviousAttempt("run-1", "success", Start, true);

        var rendered = TicketConversationPromptSection.Render(Pipeline(attempt,
            Said("alice", -30, "old ask"), Said("bob", 0, "within skew"), Said("carol", 10, "please rename it")));

        var lead = rendered.IndexOf("Since the previous attempt (run run-1", StringComparison.Ordinal);
        lead.Should().BeGreaterThan(0);
        rendered.IndexOf("please rename it", StringComparison.Ordinal).Should().BeGreaterThan(lead);
        var earlier = rendered.IndexOf("Earlier in the thread", StringComparison.Ordinal);
        earlier.Should().BeGreaterThan(rendered.IndexOf("please rename it", StringComparison.Ordinal));
        rendered.IndexOf("within skew", StringComparison.Ordinal).Should().BeGreaterThan(earlier);
        rendered.Split("please rename it").Length.Should().Be(2, "a new comment is not repeated");
    }

    [Fact]
    public void Conversation_NothingNewSinceAttempt_RendersAsBefore()
    {
        var comments = new[] { Said("alice", -30, "old ask") };

        TicketConversationPromptSection.Render(Pipeline(new PreviousAttempt("run-1", "success", Start, true), comments))
            .Should().Be(TicketConversationPromptSection.Render(comments));
    }

    [Fact]
    public void Conversation_OverCap_KeepsNewBlockFirstAndSaysDropped()
    {
        var big = new string('x', TicketConversationPromptSection.MaxChars - 40);
        var attempt = new PreviousAttempt("run-1", "success", Start, true);

        var rendered = TicketConversationPromptSection.Render(Pipeline(attempt,
            Said("alice", -30, "an old comment that no longer fits"), Said("carol", 10, big)));

        rendered.Should().Contain(big).And.NotContain("no longer fits").And.Contain("1 comment(s) omitted");
    }

    [Fact]
    public void PreviousAttempt_ResumedRow_StartsAtResumeSoEarlierCommentsAreOld()
    {
        // The stated resume gap: the row's StartedAt is the resume time, so a comment written
        // between the first leg's start and the resume is filed as earlier, not as new.
        var resumedAt = Start.AddHours(1);
        var attempt = new PreviousAttempt("run-1", "success", resumedAt, true);

        var rendered = TicketConversationPromptSection.Render(Pipeline(attempt,
            Said("alice", 30, "written during the first leg"), Said("carol", 90, "after the resume")));

        var earlier = rendered.IndexOf("Earlier in the thread", StringComparison.Ordinal);
        rendered.IndexOf("written during the first leg", StringComparison.Ordinal).Should().BeGreaterThan(earlier);
        rendered.IndexOf("after the resume", StringComparison.Ordinal).Should().BeLessThan(earlier);
    }

    [Fact]
    public async Task PreviousAttempt_ReaderThrows_RunContinues()
    {
        var reader = new Mock<IPreviousAttemptReader>();
        reader.Setup(r => r.LatestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.GetCommentsAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Said("alice", 0, "hi")]);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectName, "p");

        await new TicketExtrasFetcher(reader.Object, NullLogger<TicketExtrasFetcher>.Instance)
            .FetchAsync(provider.Object, new TicketId("42"), pipeline, CancellationToken.None);

        pipeline.Has(ContextKeys.TicketComments).Should().BeTrue();
        pipeline.Has(ContextKeys.PreviousAttempt).Should().BeFalse();
    }
}
