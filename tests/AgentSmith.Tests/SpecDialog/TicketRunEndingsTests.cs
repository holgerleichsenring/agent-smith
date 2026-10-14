using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// ticket_runs says why each attempt ended — summary, cancel reason, end time, the failed step and
/// the last abnormal sandbox end — read over a real migrated store through the real readers.
/// </summary>
public sealed class TicketRunEndingsTests : IDisposable
{
    private const string Preflight =
        "Preflight failed — the run stops here rather than discovering this an hour in:\n  - branch-state: dirty";

    private readonly TicketStoreHarness _store = new();

    [Fact]
    public async Task TicketRuns_FailedOnPreflight_CarriesTheSummaryAndTheFailedStep_MessageOnlyWhenItDiffers()
    {
        var same = Failed("r-1", Preflight, cancelReason: "operator stopped it");
        var other = Failed("r-2", "Run failed at step 2");
        await _store.AddAsync(same, other,
            Step("r-1", 0, "Fetching ticket", "success", null), Step("r-1", 2, "Preflight", "failed", Preflight),
            Step("r-1", 3, "Later", "failed", "after"),
            Step("r-2", 2, "Preflight", "failed", Preflight));

        var runs = (await AnswerAsync()).Runs;

        var first = runs.Single(r => r.RunId == "r-1");
        first.Summary.Should().Be(Preflight);
        first.CancelReason.Should().Be("operator stopped it");
        first.FinishedAt.Should().Be(TicketStoreHarness.T.AddMinutes(30));
        first.FailedStep.Should().Be(new TicketRunFailedStep(2, "Preflight", null));
        runs.Single(r => r.RunId == "r-2").FailedStep.Should().Be(new TicketRunFailedStep(2, "Preflight", Preflight));
    }

    [Fact]
    public async Task TicketRuns_SandboxVanished_CarriesTheSandboxEnd()
    {
        await _store.AddAsync(Failed("r-1", "sandbox lost"), Failed("r-2", "done"),
            Sandbox("r-1", 1, SandboxEndReasons.OomKilled, 137, true),
            Sandbox("r-1", 2, SandboxEndReasons.Vanished, null, null),
            Sandbox("r-1", 3, SandboxEndReasons.ReleasedAtEnd, 0, false),
            Sandbox("r-2", 1, SandboxEndReasons.Released, 0, false),
            Sandbox("r-2", 2, SandboxEndReasons.ReleasedAtPark, null, null));

        var runs = (await AnswerAsync()).Runs;

        runs.Single(r => r.RunId == "r-1").SandboxEnd.Should().Be(
            new TicketRunSandboxEnd(SandboxEndReasons.Vanished, null, null));
        runs.Single(r => r.RunId == "r-2").SandboxEnd.Should().BeNull();
    }

    [Fact]
    public async Task TicketRuns_SucceededRun_HasNoFailedStep()
    {
        var run = TicketStoreHarness.Run("r-1");
        run.Summary = "All specs done.";
        await _store.AddAsync(run, Step("r-1", 0, "Fetching ticket", "success", "ok"));

        var answered = (await AnswerAsync()).Runs.Single();

        answered.Summary.Should().Be("All specs done.");
        answered.FailedStep.Should().BeNull();
        answered.SandboxEnd.Should().BeNull();
        answered.CancelReason.Should().BeNull();
    }

    [Fact]
    public async Task TicketRuns_LongStepMessage_IsCutWithANote()
    {
        var message = new string('x', TicketRunText.Cap + 500);
        await _store.AddAsync(Failed("r-1", new string('s', TicketRunText.Cap + 1)),
            Step("r-1", 1, "Build", "failed", message));

        var run = (await AnswerAsync()).Runs.Single();

        run.FailedStep!.Message.Should().StartWith(new string('x', TicketRunText.Cap))
            .And.EndWith($"[cut at {TicketRunText.Cap} of {message.Length} characters]");
        run.Summary.Should().StartWith(new string('s', TicketRunText.Cap)).And.Contain("[cut at");
    }

    [Fact]
    public void TicketRunText_ShortText_IsKeptAsIs()
    {
        TicketRunText.Cut("short").Should().Be("short");
        TicketRunText.Cut(null).Should().BeNull();
    }

    public void Dispose() => _store.Dispose();

    private Task<TicketRunsResult> AnswerAsync() =>
        new BoundTicketRuns(_store.Services.GetRequiredService<FiledWorkRunsReader>(),
            ActivatorUtilities.CreateInstance<TicketRunsFacts>(_store.Services), _store.Config.Projects["alpha"], "42",
            NullLogger.Instance).ForAsync(CancellationToken.None);

    private static Run Failed(string id, string summary, string? cancelReason = null)
    {
        var run = TicketStoreHarness.Run(id, status: "failed", cancelReason: cancelReason);
        run.Summary = summary;
        return run;
    }

    private static RunStep Step(string runId, int index, string name, string status, string? message) =>
        new() { RunId = runId, StepIndex = index, StepName = name, Status = status, ResultMessage = message };

    private static RunSandbox Sandbox(string runId, long seq, string reason, int? exitCode, bool? oom) =>
        new()
        {
            RunId = runId, Key = $"{runId}-{seq}", EventSeq = seq, EndReason = reason, ExitCode = exitCode,
            OomKilled = oom,
        };
}
