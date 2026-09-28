using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.ChatRuns;

/// <summary>
/// The relational binding store and run-standing read: the two conditional writes that decide
/// which replica speaks, and what a run's row says about whether it has ended.
/// </summary>
public sealed class ChatRunBindingStoreTests : IDisposable
{
    private readonly ChatRunHarness _chat = new();

    public void Dispose() => _chat.Dispose();

    private IChatRunBindingStore Store => _chat.Get<IChatRunBindingStore>();

    [Fact]
    public void AddChatRunBinding_BindsTheRelationalStores()
    {
        Store.Should().BeOfType<DbChatRunBindingStore>();
        _chat.Get<IRunOutcomeReader>().Should().BeOfType<DbRunOutcomeReader>();
    }

    [Fact]
    public async Task TryRecordQuestion_TheSameQuestionTwice_IsWonOnce()
    {
        await Store.BindAsync(Fact("run-1", "t1"), default);

        (await Store.TryRecordQuestionAsync("run-1", "q1", "{}", default)).Should().BeTrue();
        (await Store.TryRecordQuestionAsync("run-1", "q1", "{}", default)).Should().BeFalse();
        (await Store.TryRecordQuestionAsync("run-1", "q2", "{}", default)).Should().BeTrue("a new question is new");
        (await Store.ListOpenAsync(default)).Single().QuestionId.Should().Be("q2");
    }

    [Fact]
    public async Task TryClose_IsWonOnce_AndAClosedBindingTakesNoQuestion()
    {
        await Store.BindAsync(Fact("run-1", "t1"), default);

        (await Store.TryCloseAsync("run-1", default)).Should().BeTrue();
        (await Store.TryCloseAsync("run-1", default)).Should().BeFalse();
        (await Store.TryRecordQuestionAsync("run-1", "q1", "{}", default)).Should().BeFalse();
        (await Store.ListOpenAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task FindOpenInThread_TellsThreadsApart_AndMatchesAMissingThread()
    {
        await Store.BindAsync(Fact("run-1", "t1"), default);
        await Store.BindAsync(Fact("run-2", null), default);

        (await Store.FindOpenInThreadAsync("slack", "C1", "t1", default))!.RunId.Should().Be("run-1");
        (await Store.FindOpenInThreadAsync("slack", "C1", null, default))!.RunId.Should().Be("run-2");
        (await Store.FindOpenInThreadAsync("slack", "C1", "t2", default)).Should().BeNull();
        (await Store.FindOpenInThreadAsync("teams", "C1", "t1", default)).Should().BeNull();
    }

    [Fact]
    public async Task ReadOutcome_AWaitingRunHasNotEnded_AFinishedOneCarriesItsPullRequests()
    {
        _chat.SeedRun("run-w", "waiting_for_input", finished: false);
        _chat.SeedRun("run-s", "success", finished: true, "done", "https://x/pr/2", "https://x/pr/1");
        var reader = _chat.Get<IRunOutcomeReader>();

        (await reader.ReadAsync("run-w", default))!.IsEnded.Should().BeFalse();
        var success = (await reader.ReadAsync("run-s", default))!;
        success.IsEnded.Should().BeTrue();
        success.PullRequestUrls.Should().HaveCount(2);
        (await reader.ReadAsync("run-none", default)).Should().BeNull();
    }

    private static ChatRunBindingFact Fact(string runId, string? thread) =>
        new(runId, "slack", "C1", thread, "U1", null, DateTimeOffset.UtcNow);
}
