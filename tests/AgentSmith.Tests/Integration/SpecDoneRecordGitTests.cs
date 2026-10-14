using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-10-06-03c7e: real git. The record step moves an executed spec from specs/planned/ to
/// specs/done/ with an outcome block, in one series commit with the manifest, and re-records the
/// pointer at that commit — so the directory, not a list, says the spec ran.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class SpecDoneRecordGitTests
{
    private const string Base = "2026-10-06-7c7c";
    private const string A = Base + "a";
    private const string B = Base + "b";
    private const string Ticket = "azdo-7";
    private const string Project = "sample";
    private const string RunId = "2026-10-06T09-00-00-7c7c";
    private const string Seed = "src/seed.cs";
    private static readonly RepoConnection Primary = new() { Name = "primary", Type = RepoType.Local };
    private static readonly RepoConnection Secondary = new() { Name = "secondary", Type = RepoType.Local };

    [Fact]
    public async Task RecordOutcome_ExecutedSpec_MovesToDoneWithOutcome()
    {
        await using var git = await SeriesGitRepo.CreateAsync(Seed, Base);
        if (git is null) return;
        var set = await PublishAsync(git, Set(("a", "first"), ("b", "second")));
        git.Pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery> { ["primary"] = new("default", ".", "C#") });

        var result = await RecordAsync(git, git.Pipeline, set, [Primary], new InMemorySpecSetPointerStore());

        result.IsSuccess.Should().BeTrue(result.Message);
        (await git.TrackedAsync()).Should().BeEquivalentTo(
            SeriesPaths.Manifest(Base), Done("a-first.yaml"), Done("a-first.md"),
            Planned("b-second.yaml"), Planned("b-second.md"), ".agentsmith/contexts/default/context.yaml");
        var done = File.ReadAllText(Path.Combine(git.WorkDir, Done("a-first.yaml")));
        done.Should().StartWith($"spec: {A}").And.Contain("outcome:").And.Contain($"run: {RunId}");
        (await git.GitAsync("rev-list", "--count", "HEAD")).Trim().Should().Be("3",
            "seed, publish, and ONE series commit for the record");
        (await git.GitAsync("show", "--name-status", "-M", "--format=", "HEAD"))
            .Should().Contain($"R100\t{Planned("a-first.md")}\t{Done("a-first.md")}",
                "written then removed, git still reads the move as a rename");
        File.ReadAllText(Path.Combine(git.WorkDir, ".agentsmith/contexts/default/context.yaml"))
            .Should().Contain($"{A}: ").And.Contain($"-> {Done("a-first.yaml")}");
    }

    [Fact]
    public async Task RecordOutcome_SingleSandbox_MarksExecuted()
    {
        await using var git = await SeriesGitRepo.CreateAsync(Seed, Base);
        if (git is null) return;
        var set = await PublishAsync(git, Set(("a", "first"), ("b", "second")));
        var single = new PipelineContext();
        single.Set(ContextKeys.Sandbox, git.Sandbox);
        single.Set(ContextKeys.Repository, new Repository(new BranchName(SeriesGitRepo.Branch), "/work"));
        var pointers = new InMemorySpecSetPointerStore();

        var result = await RecordAsync(git, single, set, [Primary], pointers);

        result.IsSuccess.Should().BeTrue(result.Message);
        (await pointers.GetAsync(Project, Ticket, default))!.ExecutedThrough.Should().Be(1);
        single.Get<SpecSet>(ContextKeys.SpecSet).Executed.Should().Equal(A);
        (await git.TrackedAsync()).Should().Contain(Done("a-first.yaml")).And.NotContain(Planned("a-first.yaml"));
    }

    [Fact]
    public async Task RecordOutcome_NextRun_DoesNotReadReviewerEdit()
    {
        await using var git = await SeriesGitRepo.CreateAsync(Seed, Base);
        if (git is null) return;
        var set = await PublishAsync(git, Set(("a", "first"), ("b", "second")));
        var pointers = new InMemorySpecSetPointerStore();

        await RecordAsync(git, git.Pipeline, set, [Primary], pointers);

        var read = (await SeriesDoubles.Reader(git.Files, git.Ops)
            .ReadAsync(git.Pipeline, Primary, [], new TicketKey(Ticket), default)).Read!;
        read.LastCommitSha.Should().Be((await pointers.GetAsync(Project, Ticket, default))!.RevisionSha,
            "the record is this system's own commit on the series' paths");
        read.Set.Executed.Should().Equal(A);
    }

    [Fact]
    public async Task Recut_AfterExecutedSpec_LeavesDoneUntouched()
    {
        await using var git = await SeriesGitRepo.CreateAsync(Seed, Base);
        if (git is null) return;
        var set = await PublishAsync(git, Set(("a", "first"), ("b", "second")));
        await RecordAsync(git, git.Pipeline, set, [Primary], new InMemorySpecSetPointerStore());
        var before = File.ReadAllText(Path.Combine(git.WorkDir, Done("a-first.yaml")));
        var read = (await SeriesDoubles.Reader(git.Files, git.Ops)
            .ReadAsync(git.Pipeline, Primary, [], new TicketKey(Ticket), default)).Read!.Set;

        await PublishAsync(git, read with { Specs = [read.Specs[0], Set(("b", "renamed")).Specs[0]] });

        File.ReadAllText(Path.Combine(git.WorkDir, Done("a-first.yaml"))).Should().Be(before);
        (await git.TrackedAsync()).Should().Contain([Done("a-first.yaml"), Planned("b-renamed.yaml")])
            .And.NotContain([Planned("a-first.yaml"), Planned("b-second.yaml")]);
    }

    [Fact]
    public async Task SeriesReader_PartlyExecuted_ReadsDoneAndPlanned()
    {
        await using var git = await SeriesGitRepo.CreateAsync(Seed, Base);
        if (git is null) return;
        var set = await PublishAsync(git, Set(("a", "first"), ("b", "second")));
        await RecordAsync(git, git.Pipeline, set, [Primary], new InMemorySpecSetPointerStore());
        // What a checkout-free amendment written before the record could leave beside it.
        await git.CommitFilesAsync(Planned("a-first.yaml"));

        var read = (await SeriesDoubles.Reader(git.Files, git.Ops)
            .ReadAsync(git.Pipeline, Primary, [], new TicketKey(Ticket), default)).Read!.Set;

        read.Specs.Select(p => p.SpecId).Should().Equal(A, B);
        read.Executed.Should().Equal([A], "a spec in done/ ran, its planned leftover notwithstanding");
        read.UnexecutedTail.Single().SpecId.Should().Be(B);
    }

    [Fact]
    public async Task RecordOutcome_MultiRepo_RecordsOnlyInTheCarrier()
    {
        await using var carrier = await SeriesGitRepo.CreateAsync(Seed, Base, "secondary");
        await using var other = await SeriesGitRepo.CreateAsync(Seed, Base);
        if (carrier is null || other is null) return;
        var set = await PublishAsync(carrier, Set(("a", "first")), Secondary);
        var pipeline = carrier.Pipeline;
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["primary"] = other.Sandbox, ["secondary"] = carrier.Sandbox });
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [Primary, Secondary]);
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>
            {
                ["primary"] = new("default", ".", "C#"), ["secondary"] = new("default", ".", "C#"),
            });
        pipeline.Set(ContextKeys.SpecRepo, Secondary.Name!);

        var result = await RecordAsync(carrier, pipeline, set, [Primary, Secondary], new InMemorySpecSetPointerStore());

        result.IsSuccess.Should().BeTrue(result.Message);
        (await carrier.TrackedAsync()).Should().Contain([Done("a-first.yaml"), ".agentsmith/contexts/default/context.yaml"]);
        Directory.Exists(Path.Combine(other.WorkDir, ".agentsmith")).Should().BeFalse(
            "a repository not carrying the series gets neither the file nor the index line");
    }

    // 2026-10-13-8762p: the ticket's decided defaults ride the record's one series commit — a row of the recorded
    // spec into its decision file, a row naming no spec of the set into its run's decision file.
    [Fact]
    public async Task SpecDoneRecorder_DecidedDefaults_WrittenToTheSpecsDecisionFile_InTheSeriesCommit()
    {
        await using var git = await SeriesGitRepo.CreateAsync(Seed, Base);
        if (git is null) return;
        var set = await PublishAsync(git, Set(("a", "first"), ("b", "second")));
        git.Pipeline.Set(ContextKeys.TicketId, new TicketId("7"));
        var decided = new Moq.Mock<AgentSmith.Contracts.Services.IDecidedDefaults>();
        decided.Setup(d => d.ReadAsync(Project, "7", null, Moq.It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new AgentSmith.Contracts.Decisions.DecidedDefault(11, "2026-10-05T09-00-00-0001", A, "Which queue?", "polling",
                "overruled", "use the bus", "ana", DateTimeOffset.UtcNow),
            new AgentSmith.Contracts.Decisions.DecidedDefault(12, "2026-10-05T09-00-00-0001", null, "Name?", "Widget",
                "ratified", null, "ben", DateTimeOffset.UtcNow),
        ]);
        var verdicts = new AgentSmith.Application.Services.SpecExecution.DefaultVerdictRecord(decided.Object,
            new AgentSmith.Infrastructure.Core.Services.RepositoryVerdictFile(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Infrastructure.Core.Services.RepositoryVerdictFile>.Instance),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Application.Services.SpecExecution.DefaultVerdictRecord>.Instance);

        var result = await RecordAsync(git, git.Pipeline, set, [Primary], new InMemorySpecSetPointerStore(), verdicts);

        result.IsSuccess.Should().BeTrue(result.Message);
        (await git.GitAsync("rev-list", "--count", "HEAD")).Trim().Should().Be("3", "the copy rides the ONE series commit");
        (await git.GitAsync("show", "--name-only", "--format=", "HEAD")).Should()
            .Contain($".agentsmith/decisions/{A}.yaml").And.Contain(".agentsmith/decisions/2026-10-05T09-00-00-0001.yaml");
        File.ReadAllText(Path.Combine(git.WorkDir, $".agentsmith/decisions/{A}.yaml"))
            .Should().Contain("[default #11] overruled by ana").And.Contain("Which queue? -> polling; note: use the bus");
    }

    private static async Task<SpecSet> PublishAsync(SeriesGitRepo git, SpecSet set, RepoConnection? repo = null)
    {
        var write = await SeriesDoubles.Writer(git.Files, git.Ops)
            .WriteAsync(git.Pipeline, repo ?? Primary, set, default);
        write.Written.Should().BeTrue(write.Error);
        return set;
    }

    private static Task<CommandResult> RecordAsync(
        SeriesGitRepo git, PipelineContext pipeline, SpecSet set, IReadOnlyList<RepoConnection> repos,
        InMemorySpecSetPointerStore pointers, AgentSmith.Application.Services.SpecExecution.DefaultVerdictRecord? verdicts = null)
    {
        pipeline.Set(ContextKeys.RunId, RunId);
        pipeline.Set(ContextKeys.ProjectName, Project);
        pipeline.Set(ContextKeys.SpecSet, set);
        pipeline.Set(ContextKeys.CurrentSpec, set.Specs[0].Draft);
        return SeriesDoubles.RecordHandler(git.Files, pointers, git.Ops, verdicts: verdicts).ExecuteAsync(
            new WriteSpecRecordContext(pipeline.Get<Repository>(ContextKeys.Repository), pipeline, repos), default);
    }

    private static string Planned(string rest) => $"{SeriesPaths.Planned}/{Base}{rest}";

    private static string Done(string rest) => $"{SeriesPaths.Done}/{Base}{rest}";

    private static SpecSet Set(params (string Letter, string Label)[] specs) => new(
        Ticket,
        [.. specs.Select(s => new DerivedSpec(
            new SpecDraft($"{Base}{s.Letter}", $"Goal {s.Letter}",
                $"spec: {Base}{s.Letter}\ngoal: \"Goal {s.Letter} {s.Label}\"\ndone:\n  - \"Done.\"\n", [])
            { Done = ["Done."] },
            s.Label, $"# {s.Label}\n\nThe ticket's own words for {s.Label}.\n", []))],
        SpecAccounting.Empty,
        [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)],
        SpecSource.Derived,
        Series: Base);
}
