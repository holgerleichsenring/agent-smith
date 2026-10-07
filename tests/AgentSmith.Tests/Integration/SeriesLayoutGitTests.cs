using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-10-06-03c7d: real git. A series is one manifest under series/ and its specs in the shared
/// specs/planned/; a revision removes exactly the planned files of its own series it no longer
/// renders, and the revision sha moves with an edit to any of them.
/// </summary>
[Collection(ExternalProcessCollection.Name)]
public sealed class SeriesLayoutGitTests
{
    private const string Base = "2026-10-06-4b4b";
    private const string Ticket = "azdo-4";
    private const string Other = ".agentsmith/specs/planned/2026-10-06-9c9ca-another-series.yaml";
    private static readonly RepoConnection Repo = new() { Name = "primary", Type = RepoType.Local };

    [Fact]
    public async Task SeriesWriter_DroppedSpec_RemovesOnlyItsFiles()
    {
        await using var git = await SeriesGitRepo.CreateAsync(Other, Base);
        if (git is null) return;
        await WriteAsync(git, Set(("a", "first"), ("b", "second")));

        await WriteAsync(git, Set(("a", "first")));

        (await git.TrackedAsync()).Should().BeEquivalentTo(
            SeriesPaths.Manifest(Base), Planned("a-first.yaml"), Planned("a-first.md"), Other);
    }

    [Fact]
    public async Task SeriesWriter_RelabelledSpec_RemovesOldFile()
    {
        await using var git = await SeriesGitRepo.CreateAsync(Other, Base);
        if (git is null) return;
        await WriteAsync(git, Set(("a", "first")));

        await WriteAsync(git, Set(("a", "renamed")));

        (await git.TrackedAsync()).Should().BeEquivalentTo(
            SeriesPaths.Manifest(Base), Planned("a-renamed.yaml"), Planned("a-renamed.md"), Other);
    }

    [Fact]
    public async Task SeriesWriter_AfterAmendment_RemovesLeftovers()
    {
        await using var git = await SeriesGitRepo.CreateAsync(Other, Base);
        if (git is null) return;
        // What a checkout-free amendment leaves: it writes, it cannot delete.
        await git.CommitFilesAsync(Planned("a-old-label.yaml"), Planned("c-dropped.yaml"), Planned("c-dropped.md"));

        await WriteAsync(git, Set(("a", "first"), ("b", "second")));

        (await git.TrackedAsync()).Should().NotContain(
            [Planned("a-old-label.yaml"), Planned("c-dropped.yaml"), Planned("c-dropped.md")])
            .And.Contain(Other, "another series' file is never this cut's to remove");
    }

    [Fact]
    public async Task SeriesReader_ReviewerEditsSpecFile_ShaChanges()
    {
        await using var git = await SeriesGitRepo.CreateAsync(Other, Base);
        if (git is null) return;
        var written = await WriteAsync(git, Set(("a", "first")));
        var reader = SeriesDoubles.Reader(git.Files, git.Ops);
        (await reader.ReadAsync(git.Pipeline, Repo, new TicketKey(Ticket), default)).Read!.LastCommitSha
            .Should().Be(written, "the publish's commit is the series' last");
        await git.CommitFilesAsync("src/unrelated.cs");
        (await reader.ReadAsync(git.Pipeline, Repo, new TicketKey(Ticket), default)).Read!.LastCommitSha
            .Should().Be(written, "a coding commit is no revision");

        await git.CommitFilesAsync(Planned("a-first.yaml"));

        var read = (await reader.ReadAsync(git.Pipeline, Repo, new TicketKey(Ticket), default)).Read!;
        read.LastCommitSha.Should().Be(await git.HeadAsync(), "a reviewer's edit of a spec file is a new revision")
            .And.NotBe(written);
    }

    private static async Task<string> WriteAsync(SeriesGitRepo git, SpecSet set)
    {
        var write = await SeriesDoubles.Writer(git.Files, git.Ops).WriteAsync(git.Pipeline, Repo, set, default);
        write.Written.Should().BeTrue(write.Error);
        return write.CommitSha!;
    }

    private static string Planned(string rest) => $"{SeriesPaths.Planned}/{Base}{rest}";

    private static SpecSet Set(params (string Letter, string Label)[] specs) => new(
        Ticket,
        [.. specs.Select(s => new SpecPhase(
            new PhaseDraft($"{Base}{s.Letter}", $"Goal {s.Letter}",
                $"spec: {Base}{s.Letter}\ngoal: \"Goal {s.Letter} {s.Label}\"\ndone:\n  - \"Done.\"\n", [])
            { Done = ["Done."] },
            s.Label, $"# {s.Label}\n", []))],
        SpecAccounting.Empty,
        [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)],
        SpecSource.Derived,
        Series: Base);
}
