using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs.Branch;

/// <summary>
/// 2026-11-17-a0b6a: a filed ticket's set is read back off its branch on the host — no sandbox — by the
/// same sub-series reading and composition a run's checkout read uses.
/// </summary>
public sealed class TicketBranchSeriesTests
{
    private const string Key = "azuredevops-1";

    [Fact]
    public async Task ReadAsync_AFiledSet_ReadsBackItsGoalStepsDoneAndYaml()
    {
        var (reader, _) = await FiledAsync(Specs(("p9000a", "sample-api")));

        var read = await reader.ReadAsync(Project(), new TicketKey(Key), new TicketId("1"), default);

        read.State.Should().Be(SpecSetBranchState.Answered, read.Why);
        var spec = read.Set!.Specs.Should().ContainSingle().Subject;
        spec.SpecId.Should().Be("p9000a");
        spec.Draft.Goal.Should().Be("Read the set off the branch");
        spec.Draft.Steps.Should().ContainSingle().Which.Id.Should().Be("read");
        spec.Draft.Done.Should().Equal("The set reads back.");
        spec.Draft.Yaml.Should().Contain("spec: p9000a");
        read.Read!.LastCommitSha.Should().Be(RecordingBranchSources.DefaultSha, "the pointer names the sha filing wrote");
    }

    [Fact]
    public async Task ReadAsync_TwoRepositories_JoinsBothSubSeries()
    {
        var (reader, _) = await FiledAsync(Specs(("p9000a", "sample-api"), ("p9000b", "sample-web")));

        var read = await reader.ReadAsync(Project(), new TicketKey(Key), new TicketId("1"), default);

        read.State.Should().Be(SpecSetBranchState.Answered, read.Why);
        read.Set!.Specs.Select(s => s.SpecId).Should().Equal("p9000a", "p9000b");
    }

    [Fact]
    public async Task ReadAsync_NoPointer_IsNothing()
    {
        var reader = Reader(new RecordingBranchSources(), new InMemorySpecSetPointerStore());

        var read = await reader.ReadAsync(Project(), new TicketKey(Key), new TicketId("1"), default);

        read.State.Should().Be(SpecSetBranchState.NothingAtThePath);
    }

    [Fact]
    public async Task ReadAsync_AHostThatThrows_IsUnreadableNamingTheRepository()
    {
        var (reader, sources) = await FiledAsync(Specs(("p9000a", "sample-api")));
        sources.ReadThrows = new HttpRequestException("503 from the host");

        var read = await reader.ReadAsync(Project(), new TicketKey(Key), new TicketId("1"), default);

        read.State.Should().Be(SpecSetBranchState.Unreadable);
        read.Why.Should().Contain("agent-smith/1").And.Contain("503 from the host");
    }

    private static async Task<(TicketBranchSeries Reader, RecordingBranchSources Sources)> FiledAsync(
        IReadOnlyList<DerivedSpec> specs)
    {
        var sources = new RecordingBranchSources();
        var pointers = new InMemorySpecSetPointerStore();
        var record = new SpecApprovalRecord(Key,
            new SpecSet(Key, specs, SpecAccounting.Empty, [], SpecSource.Approved,
                Approval: new SpecApproval(DateTimeOffset.UnixEpoch, "job-1", "sample.user"), Series: "2026-11-17-0b0b"),
            ["sample-api", "sample-web"], "sample-tracker", "sample-api");
        var written = await ApprovedSetDoubles.Branch(sources, pointers)
            .WriteAsync(Project(), record, new TicketId("1"), Stored(), default);
        written.Written.Should().BeTrue(written.Error);
        return (Reader(sources, pointers), sources);
    }

    internal static TicketBranchSeries Reader(RecordingBranchSources sources, ISpecSetPointerStore pointers) =>
        new(pointers, sources, SeriesDoubles.SubSeries(), SeriesDoubles.Composer(), NullLogger<TicketBranchSeries>.Instance);

    internal static IReadOnlyList<DerivedSpec> Specs(params (string Id, string Repo)[] specs) =>
        [.. specs.Select(s => new DerivedSpec(new SpecDraftReader().Read(Yaml(s.Id, s.Repo)), "read-back", string.Empty, []))];

    internal static string Yaml(string id, string repo) => $"""
        spec: {id}
        goal: "Read the set off the branch"
        repos: [{repo}]
        steps:
          - id: read
            action: "Read it."
        done:
          - "The set reads back."
        """;

    internal static Ticket Stored() => new(new TicketId("1"), "the tracker's title", "<p>body</p>", null, "open", "journalling", []);

    internal static ResolvedProject Project() => new()
    {
        Name = "proj",
        Tracker = new TrackerConnection { Name = "sample-tracker", Type = TrackerType.AzureDevOps },
        Repos = [new RepoConnection { Name = "sample-api" }, new RepoConnection { Name = "sample-web" }],
    };
}
