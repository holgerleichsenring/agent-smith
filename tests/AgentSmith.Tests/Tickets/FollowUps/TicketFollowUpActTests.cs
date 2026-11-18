using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets.FollowUps;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.FollowUps;

/// <summary>2026-11-18-c8e2d: a follow-up a spec's master proposed, filed by a person: read from the stored report,
/// filed once through the 'file' run as a FilingFollowUp.</summary>
public sealed class TicketFollowUpActTests : IDisposable
{
    private readonly Mock<IFilingRun> _filing = new();
    private readonly TicketStoreHarness _store;

    public TicketFollowUpActTests() =>
        _store = new TicketStoreHarness(s => s.AddSingleton(_filing.Object).AddScoped<TicketFollowUpAct>());

    [Fact]
    public async Task FileAsync_AProposedFollowUp_FilesItOnceAsAFollowUpTicket()
    {
        await SeedAsync();
        FilingRequest? sent = null;
        _filing.Setup(f => f.RunRequestAsync("alpha", It.IsAny<FilingRequest>()))
            .Callback<string, FilingRequest>((_, r) => sent = r)
            .ReturnsAsync(new FilingReport([new FiledTicket("https://t/43", "Exports stream CSV") { TicketId = "43", Key = "#43" }], null));

        var first = await Act().FileAsync("alpha", "42", new TicketFollowUpRequest("r-1", "s2", 0), "Ada", default);
        var second = await Act().FileAsync("alpha", "42", new TicketFollowUpRequest("r-1", "s2", 0), "Ada", default);

        first.Refusal.Should().BeNull();
        sent!.FollowUp!.Title.Should().Be("Exports stream CSV");
        sent.FollowUp.Body.Should().Contain("paging first").And.Contain("spec s2 in run r-1");
        sent.Principal.Should().Be("Ada");
        second.Status.Should().Be(409, "the ledger key is spent");
        _filing.Verify(f => f.RunRequestAsync(It.IsAny<string>(), It.IsAny<FilingRequest>()), Times.Once);
    }

    [Fact]
    public async Task FileAsync_NoSuchFollowUp_OrAnotherTicketsRun_IsRefused()
    {
        await SeedAsync();

        (await Act().FileAsync("alpha", "42", new TicketFollowUpRequest("r-1", "s2", 3), "Ada", default)).Status.Should().Be(404);
        (await Act().FileAsync("alpha", "99", new TicketFollowUpRequest("r-1", "s2", 0), "Ada", default)).Status.Should().Be(404);
        _filing.VerifyNoOtherCalls();
    }

    private TicketFollowUpAct Act() => _store.Services.CreateScope().ServiceProvider.GetRequiredService<TicketFollowUpAct>();

    private async Task SeedAsync()
    {
        var spec = Spec("r-1", "s2", "done");
        spec.AcceptanceJson = RunStoryJson.Serialize(new AcceptanceView([], "verbatim", "the ratified spec", AcceptanceSources.MasterVerification,
            FollowUps: [new FollowUpView("Exports stream CSV", "paging first", "s2")]));
        await _store.AddAsync(Run("r-1"), spec);
    }

    public void Dispose() => _store.Dispose();
}
