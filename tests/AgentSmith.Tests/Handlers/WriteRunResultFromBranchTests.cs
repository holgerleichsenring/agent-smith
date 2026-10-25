using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Dialogue;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-13-3890b: a run that commits its own work reports the files its branch carries —
/// not the last master pass's tool writes — its wall-clock duration, and its failure once.
/// </summary>
public sealed class WriteRunResultFromBranchTests
{
    private const string Failure = "dotnet test failed: 3 tests red in Api.Tests";
    private readonly Dictionary<ISandbox, InMemorySandboxFileReader> _readers = new();
    private readonly PipelineContext _pipeline = new();
    private readonly Dictionary<string, ISandbox> _sandboxes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _owners = new(StringComparer.Ordinal);

    [Fact]
    public async Task RunResult_FilesFromACheckpointedPass_AreListed()
    {
        Sandbox("app", "app", new GitScriptSandbox("M\tsrc/Early.cs\nA\tsrc/New.cs\nM\tsrc/Late.cs\n"));

        var md = await ResultAsync("code", [Change("src/Late.cs")]);

        md.Should().Contain("- [Modify] src/Early.cs").And.Contain("- [Create] src/New.cs")
            .And.Contain("- [Modify] src/Late.cs").And.Contain("Completed 3 change(s).");
    }

    [Fact]
    public async Task RunResult_SecondarySandboxAndUntrackedFiles_AreListed()
    {
        Sandbox("app", "app", new GitScriptSandbox("M\tsrc/A.cs\n", untracked: "src/Fresh.cs\n"));
        Sandbox("app-node", "app", new GitScriptSandbox("M\tweb/b.ts\n"));

        var md = await ResultAsync("code", []);

        md.Should().Contain("- [Modify] src/A.cs").And.Contain("- [Create] src/Fresh.cs")
            .And.Contain("- [Modify] web/b.ts");
    }

    [Fact]
    public async Task RunResult_PrReview_KeepsToolWrites()
    {
        Sandbox("app", "app", new GitScriptSandbox("M\tsrc/TheirWork.cs\n"));

        var md = await ResultAsync("pr-review", [Change("src/Fix.cs")]);

        md.Should().Contain("- [Modify] src/Fix.cs").And.NotContain("TheirWork.cs");
    }

    [Fact]
    public async Task RunResult_MadDiscussion_UsesTheBranch()
    {
        Sandbox("app", "app", new GitScriptSandbox("A\tdocs/discussion.md\n"));

        var md = await ResultAsync("mad-discussion", []);

        md.Should().Contain("- [Create] docs/discussion.md");
    }

    [Fact]
    public async Task LedgerCoverage_TargetInAnotherRepository_NoWarning()
    {
        Sandbox("api", "api", new GitScriptSandbox("M\tsrc/a.cs\n"));
        Sandbox("web", "web", new GitScriptSandbox("M\tsrc/b.ts\n"));
        Ledger("web/src/b.ts", "src/a.cs", "api/src/missing.cs");

        await ResultAsync("code", []);

        var all = string.Join("\n", _readers.Values.SelectMany(r => r.Files)
            .Where(f => f.Key.EndsWith("result.md")).Select(f => f.Value));
        Regex.Matches(all, @"\[progress-ledger\][^\n]*").Select(m => m.Value)
            .Should().OnlyContain(line => line.Contains("api/src/missing.cs")).And.HaveCount(2);
    }

    [Fact]
    public async Task LedgerCoverage_SingleRepoPrefixedTarget_NoWarning()
    {
        Sandbox("app", "app", new GitScriptSandbox("M\tsrc/a.cs\n"));
        Ledger("app/src/a.cs", "src/a.cs");

        var md = await ResultAsync("code", []);

        md.Should().NotContain("[progress-ledger]");
    }

    [Fact]
    public async Task RunResult_OneSandboxUnreadable_OthersKeepTheirLists()
    {
        Sandbox("app", "app", new GitScriptSandbox("M\tsrc/A.cs\n"));
        Sandbox("app-node", "app", GitScriptSandbox.Unreadable("node-job"));

        var md = await ResultAsync("code", [Change("src/Tool.cs")]);

        md.Should().Contain("- [Modify] src/A.cs").And.Contain("- [Modify] src/Tool.cs")
            .And.Contain("[changed-files] the branch in sandbox 'app-node' could not be read");
    }

    [Fact]
    public async Task RunResult_Duration_IsWallClock()
    {
        Sandbox("app", "app", new GitScriptSandbox(""));
        _pipeline.Set(ContextKeys.RunStartedAt, DateTimeOffset.UtcNow.AddMinutes(-49));

        var md = await ResultAsync("code", []);

        md.Should().MatchRegex(@"duration_seconds: 294[01]\b");
    }

    [Fact]
    public async Task RunResult_MechanicalFailure_RenderedOnceInResultMd()
    {
        Sandbox("api", "api", new GitScriptSandbox("M\tsrc/a.cs\n"));
        Sandbox("web", "web", new GitScriptSandbox("M\tsrc/b.ts\n"));
        _pipeline.Set(ContextKeys.FailureReason, Failure);
        RunAccountLedger.RecordProblem(_pipeline, ["api", "web"], Failure);

        var md = await ResultAsync("code", [], repo: "api");

        Regex.Matches(md, Regex.Escape(Failure)).Should().HaveCount(1);
        md.Should().Contain("- no account could be taken — see Outcome");
        Regex.Matches(RunAccountSection.Build(_pipeline), Regex.Escape(Failure)).Should().HaveCount(2,
            "the pull request body and the ticket comment carry no Outcome to point at");
    }

    [Fact]
    public async Task RunResult_FailedRun_SummaryDoesNotSayCompleted()
    {
        Sandbox("app", "app", new GitScriptSandbox("M\tsrc/a.cs\n"));
        _pipeline.Set(ContextKeys.FailureReason, Failure);

        var md = await ResultAsync("code", []);

        md.Should().NotContain("Completed").And.Contain("Did not complete; 1 file(s) changed on the branch.");
    }

    private void Sandbox(string key, string repo, ISandbox sandbox)
    {
        _sandboxes[key] = sandbox;
        _owners[key] = repo;
        _readers[sandbox] = new InMemorySandboxFileReader();
    }

    private void Ledger(params string[] doneTargets) =>
        _pipeline.Set(ContextKeys.ProgressLedger, new ProgressLedger(
            [.. doneTargets.Select((t, i) => new ProgressLedgerEntry($"{i + 1}", $"step {i + 1}", ProgressStatus.Done, t))]));

    private static CodeChange Change(string path) => new(new FilePath(path), "x", "Modify");

    private async Task<string> ResultAsync(string pipelineName, List<CodeChange> toolWrites, string? repo = null)
    {
        _pipeline.Set(ContextKeys.RunId, "2026-10-13T10-00-00-3890");
        _pipeline.Set(ContextKeys.PipelineName, pipelineName);
        _pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, _sandboxes);
        _pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, _owners);
        _pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [.. _owners.Values.Distinct().Select(name => new RepoConnection { Name = name })]);
        var ticket = new Ticket(new TicketId("42"), "Add login feature", "Description", null, "Open", "github");
        await Handler().ExecuteAsync(new WriteRunResultContext(
            new Repository(new BranchName("feature/x"), "https://github.com/test/test"), null, ticket,
            toolWrites, _pipeline), CancellationToken.None);
        var primary = _sandboxes[_owners.First(o => o.Value == (repo ?? o.Value)).Key];
        return _readers[primary].Files.Single(f => f.Key.EndsWith("result.md")).Value;
    }

    private WriteRunResultHandler Handler()
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns<ISandbox>(s => _readers[s]);
        return new WriteRunResultHandler(
            factory.Object, new InMemoryDialogueTrail(), new InMemoryRunArtifactStore(),
            new Events.RecordingEventPublisher(),
            new RunNarrativeMemoryWriter(NullLogger<RunNarrativeMemoryWriter>.Instance), AgentSmith.Tests.TestHelpers.MemoryOutcomes.None(),
            new SandboxTargets(), TestRunResultChanges.Create(), NullLogger<WriteRunResultHandler>.Instance);
    }
}
