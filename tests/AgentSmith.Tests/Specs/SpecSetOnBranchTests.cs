using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-22-6ad7: the reader answers WHY it found no set. Four situations used to share one
/// null, and the caller's answer to two of them differs: an unwritten branch is handed the
/// approval record's set once, a branch it could not read is handed back.
/// </summary>
public sealed class SpecSetOnBranchTests
{
    private const string Key = "azdo-19106";
    private const string Base = "2026-10-06-0a0a";

    private static string Manifest(string ticket, string id) => $"ticket: {ticket}\nspecs:\n- {id}\n";

    private static string Spec(string id) => $"spec: {id}\ngoal: g\ndone:\n  - d\n";

    [Fact]
    public async Task SeriesReader_NothingAtThePath_SaysNothingIsAtThePath()
    {
        var result = await ReadAsync(new SpecBranchFiles { Key = Key });

        result.State.Should().Be(SpecSetBranchState.NothingAtThePath);
        result.Read.Should().BeNull();
    }

    [Fact]
    public async Task SeriesReader_AManifestNamingTheTicketThatDoesNotParse_IsUnreadableRatherThanAbsent()
    {
        var branch = new SpecBranchFiles { Key = Key };
        branch.Seed($".agentsmith/series/{Base}.yaml", $"ticket: {Key}\nspecs: [this is not\n  a document");

        var result = await ReadAsync(branch);

        result.State.Should().Be(SpecSetBranchState.Unreadable,
            "a file that is there and broke is an edit gone wrong, not a hand-off that never happened");
        result.Why.Should().Contain("did not parse");
    }

    [Fact]
    public async Task SeriesReader_AListedSpecWithNoFile_IsUnreadable()
    {
        var branch = new SpecBranchFiles { Key = Key };
        branch.SeedSeries(Base, Manifest(Key, $"{Base}a"), new Dictionary<string, string>());

        var result = await ReadAsync(branch);

        result.State.Should().Be(SpecSetBranchState.Unreadable);
        result.Why.Should().Contain($"{Base}a");
    }

    [Fact]
    public async Task SeriesReader_FindsManifestByTicketKey()
    {
        var branch = new SpecBranchFiles { Key = Key };
        branch.SeedSeries("2026-10-06-9e9e", Manifest("azdo-77", "2026-10-06-9e9ea"),
            new Dictionary<string, string> { ["2026-10-06-9e9ea-other"] = Spec("2026-10-06-9e9ea") });
        branch.SeedSeries(Base, Manifest(Key, $"{Base}a"),
            new Dictionary<string, string> { [$"{Base}a-first"] = Spec($"{Base}a") });

        var result = await ReadAsync(branch);

        result.State.Should().Be(SpecSetBranchState.Answered);
        result.Set!.Series.Should().Be(Base, "the base is the manifest's file name");
        result.Set.Key.Should().Be(Key);
        result.Set.Phases.Should().ContainSingle().Which.FileStem.Should().Be($"{Base}a-first",
            "the label is read off the file name");
    }

    [Fact]
    public async Task SeriesReader_TwoManifestsNamingOneTicket_IsUnreadable()
    {
        var branch = new SpecBranchFiles { Key = Key };
        branch.SeedSeries(Base, Manifest(Key, $"{Base}a"), new Dictionary<string, string>());
        branch.SeedSeries("2026-10-06-9e9e", Manifest(Key, "2026-10-06-9e9ea"), new Dictionary<string, string>());

        var result = await ReadAsync(branch);

        result.State.Should().Be(SpecSetBranchState.Unreadable);
        result.Why.Should().Contain("2 manifests");
    }

    [Fact]
    public async Task SeriesReader_TwoFilesForOneId_IsUnreadable()
    {
        var branch = new SpecBranchFiles { Key = Key };
        branch.SeedSeries(Base, Manifest(Key, $"{Base}a"), new Dictionary<string, string>
        {
            [$"{Base}a-old-label"] = Spec($"{Base}a"),
            [$"{Base}a-new-label"] = Spec($"{Base}a"),
        });

        var result = await ReadAsync(branch);

        result.State.Should().Be(SpecSetBranchState.Unreadable, "which file is the spec is not the reader's guess");
        result.Why.Should().Contain("old-label").And.Contain("new-label");
    }

    /// <summary>
    /// 2026-10-06-03c7d: a set.yaml under a per-ticket directory is not where a series lies; in-flight
    /// work in the old layout is ignored and the ticket is derived whole.
    /// </summary>
    [Fact]
    public async Task SeriesReader_AnOldSetUnderTheTicketDirectory_IsReadAsAbsent()
    {
        var branch = new SpecBranchFiles { Key = Key };
        branch.Seed($".agentsmith/specs/{Key}/set.yaml", $"key: {Key}\nseries: {Base}\nphases:\n- p19106a-first\n");

        var result = await ReadAsync(branch);

        result.State.Should().Be(SpecSetBranchState.NothingAtThePath);
    }

    /// <summary>
    /// The path was never LOOKED at, so "nothing is there" is a claim this run cannot make — and
    /// a branch it cannot see may well carry the edit a copy would hide.
    /// </summary>
    [Fact]
    public async Task SeriesReader_NoSandboxForTheCarryingRepository_IsUnreadableRatherThanAbsent()
    {
        var result = await ReadAsync(new SpecBranchFiles { Key = Key }, sandboxed: false);

        result.State.Should().Be(SpecSetBranchState.Unreadable);
        result.Why.Should().Contain("primary").And.Contain("not checked out");
    }

    private static async Task<SpecSetOnBranch> ReadAsync(
        SpecBranchFiles branch, bool sandboxed = true)
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.Empty, 0, false, 0, null, "branch-sha"));
        var readers = new Mock<ISandboxFileReaderFactory>();
        readers.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(branch);
        var pipeline = new PipelineContext();
        if (sandboxed)
            pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["primary"] = sandbox.Object });
        var reader = SeriesDoubles.Reader(readers.Object);
        return await reader.ReadAsync(
            pipeline, new RepoConnection { Name = "primary" }, new TicketKey(Key), default);
    }
}
