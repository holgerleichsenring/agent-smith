using AgentSmith.Application;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure;
using AgentSmith.Tests.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Validation.TicketGraphFixtures;

namespace AgentSmith.Tests.Validation;

/// <summary>
/// 2026-10-14-dbcej: a design reply that proposes a list of tickets — resolved whole, refused with the reason
/// named, or, cut off at the output limit, re-prompted instead of kept as an answer.
/// </summary>
public sealed class TicketGraphResolverTests
{
    private static OutcomeProposalResolver Resolver()
    {
        var validator = new SpecDraftValidator(new SpecSchemaProvider());
        var reader = new SpecDraftReader();
        return new OutcomeProposalResolver(
            validator, reader, new BugOutcomeParser(),
            new EpicOutcomeParser(validator, reader, new RequiresEdgeChecker(), DialogTestRepos.Real(), SeriesContracts.Reader(), SeriesSettingsDoubles.Reader()),
            Parser(), DialogTestRepos.Real());
    }

    private static string Error(string reply) =>
        Resolver().Resolve(reply, RepoScope).Should().BeOfType<OutcomeInvalid>().Subject.Error;

    [Fact]
    public void OutcomeProposalResolver_TicketsBlock_ResolvesTicketGraphWithEveryField()
    {
        var reply = Block(
            "  - key: \"orders-api\"\n    title: \"Orders: the fulfilment state\"\n    requirement: |\n      The API returns the state.\n"
            + "      Nothing else changes.\n    done:\n      - \"GET /orders returns: the state\"\n      - \"PATCH moves it\"\n"
            + "    requires: [\"ticket:#41\"]\n    repos: [\"api\"]\n"
            + Item("orders-ui", "[\"orders-api\"]", "    repos: [\"web\"]\n"));

        var graph = Resolver().Resolve(reply, RepoScope).Should().BeOfType<OutcomeResolved>()
            .Which.Proposal.Should().BeOfType<TicketGraphOutcome>().Subject;

        graph.Tickets.Should().HaveCount(2);
        graph.Tickets[0].Should().BeEquivalentTo(new GraphTicket(
            "orders-api", "Orders: the fulfilment state", "The API returns the state.\nNothing else changes.",
            ["GET /orders returns: the state", "PATCH moves it"], ["ticket:#41"], ["api"]));
        graph.Tickets[1].Requires.Should().Equal("orders-api");
        graph.Tickets[1].Repos.Should().Equal("web");
    }

    [Fact]
    public void OutcomeProposalResolver_UnknownKind_NamesBugEpicAndTickets()
    {
        Error("```outcome\nkind: graph\n```").Should().Be("unknown outcome kind 'graph' — expected bug, epic or tickets");
        Error("```outcome\ntitle: \"t\"\n```").Should().Contain("(bug | epic | tickets)");
    }

    [Fact]
    public void OutcomeProposalResolver_UnclosedOutcomeFence_IsInvalidNamingTheOutputLimit()
    {
        var cut = Block(Item("orders-api") + Item("orders-ui"))[..^30];

        Error(cut).Should().Contain("not closed").And.Contain("cut at the output limit").And.Contain("propose fewer tickets");
    }

    [Fact]
    public void OutcomeProposalResolver_ProseWithoutAnyFence_IsStillAnAnswer() =>
        Resolver().Resolve("I would put the list in an ```outcome block once we agree.", RepoScope)
            .Should().BeOfType<OutcomeResolved>().Which.Proposal.Should().BeOfType<AnswerOutcome>();

    [Fact]
    public void OutcomeProposalResolver_TitleWithUnquotedColon_IsInvalidYamlAndAsksToQuote() =>
        Error(Block("  - key: \"orders-api\"\n    title: Orders: schema slice\n    requirement: \"r\"\n    done: [\"d\"]\n" + Item("orders-ui")))
            .Should().Contain("is not valid YAML").And.EndWith("quote every string that contains ': '");

    [Fact]
    public void OutcomeProposalResolver_TwentyRealisticTicketTexts_ResolveWhole()
    {
        var reply = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Validation", "fixtures", "ticket-graph-20-tickets.md"));
        var block = SpecDialogDraftBlocks.OutcomeBlocks(reply).Single().Value;

        block.Length.Should().BeLessThanOrEqualTo(24_000, "twenty realistic ticket texts fit the 8192-token output cap");
        var graph = Resolver().Resolve(reply, RepoScope).Should().BeOfType<OutcomeResolved>()
            .Which.Proposal.Should().BeOfType<TicketGraphOutcome>().Subject;
        graph.Tickets.Should().HaveCount(TicketGraphLimits.MaxTickets);
        graph.Tickets.Should().AllSatisfy(t => t.Done.Should().HaveCount(4));
    }

    [Fact]
    public void OutcomeProposalResolver_FromRealServiceCollection_ParsesATicketsBlock()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddAgentSmithInfrastructure();
        services.AddAgentSmithCommands();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOutcomeProposalResolver>()
            .Resolve(Block(Item("orders-api") + Item("orders-ui", "[\"orders-api\"]")), RepoScope)
            .Should().BeOfType<OutcomeResolved>().Which.Proposal.Should().BeOfType<TicketGraphOutcome>()
            .Which.Tickets.Select(t => t.Key).Should().Equal("orders-api", "orders-ui");
    }
}
