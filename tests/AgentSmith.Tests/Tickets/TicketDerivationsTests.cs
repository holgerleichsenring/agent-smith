using AgentSmith.Contracts.Commands;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Tickets.NeverRun.NeverRunFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>2026-10-26-bdd0f: the ticket detail lists each spec derivation apart from the attempts, with what came of it.</summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class TicketDerivationsTests : IDisposable
{
    private const string Attempt = "2026-10-13T09-00-00-0001";
    private const string Derived = "2026-10-13T10-00-00-0002";
    private const string Asked = "2026-10-13T11-00-00-0003";
    private const string Failed = "2026-10-13T12-00-00-0004";
    private const string Running = "2026-10-13T13-00-00-0005";
    private readonly TicketStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task TicketDetail_SpecDerive_IsADerivationNotAnAttempt()
    {
        await _store.AddAsync(Run(Attempt), Step(Attempt), Derivation(Derived, "success", "2 specs derived and stored on the ticket branch."),
            Cut(Derived, specs: 2), new RunRepo { RunId = Derived, RepoName = "api", PrUrl = "https://example.test/pr/7" });

        var detail = (await _store.Detail.ReadAsync("alpha", "42", CancellationToken.None))!;

        detail.Attempts.Select(a => a.RunId).Should().Equal(Attempt);
        var derivation = detail.Derivations.Should().ContainSingle().Subject;
        (derivation.Outcome, derivation.SpecCount).Should().Be((TicketDerivationOutcomes.Derived, 2));
        derivation.PullRequests.Should().Equal("https://example.test/pr/7");
        derivation.Summary.Should().Be("2 specs derived and stored on the ticket branch.");
    }

    [Fact]
    public async Task TicketDerivations_Outcomes_DerivedAskedNotFinishedRunning()
    {
        await _store.AddAsync(Run(Attempt), Step(Attempt),
            Derivation(Derived, "success", "1 spec derived as a preview; nothing was written."), Cut(Derived, specs: 1),
            Derivation(Asked, "success", "Spec derivation stopped: two readings"), Cut(Asked, specs: 0, handback: "Question"),
            Derivation(Failed, "failed", "The derivation could not read the repository"),
            Derivation(Running, "running", "starting: spec derivation requested"));

        var derivations = (await _store.Detail.ReadAsync("alpha", "42", CancellationToken.None))!.Derivations!;

        derivations.Select(d => (d.RunId, d.Outcome)).Should().Equal(
            (Running, TicketDerivationOutcomes.Running), (Failed, TicketDerivationOutcomes.NotFinished),
            (Asked, TicketDerivationOutcomes.Asked), (Derived, TicketDerivationOutcomes.Derived));
        derivations.Single(d => d.RunId == Derived).PullRequests.Should().BeEmpty("a preview opens no pull request");
    }

    [Fact]
    public async Task TicketDetail_FiledTicketWithDerivation_ReturnsItsNeverRunDetail()
    {
        await _store.AddAsync(Filing("59"), Derivation(Derived, "success", "1 spec derived and stored on the ticket branch.", "59"),
            Cut(Derived, specs: 1));

        var detail = (await _store.Detail.ReadAsync("alpha", "59", CancellationToken.None))!;

        detail.Attempts.Should().BeEmpty();
        detail.Derivations.Should().ContainSingle().Which.RunId.Should().Be(Derived);
    }

    private static Run Derivation(string id, string status, string summary, string ticket = "42")
    {
        var run = Run(id, ticket: ticket, status: status, pipeline: PipelinePresets.SpecDeriveName);
        run.Summary = summary;
        return run;
    }

    private static RunSeriesCut Cut(string runId, int specs, string? handback = null) => new()
    {
        RunId = runId, SetKey = "github-42", HandbackCase = handback, At = T,
        SpecsJson = "[" + string.Join(",", Enumerable.Range(1, specs).Select(i => $"{{\"SpecId\":\"s{i}\"}}")) + "]",
    };
}
