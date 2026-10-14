using System.Text.RegularExpressions;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Specs.Contract;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Contract;

/// <summary>
/// 2026-10-13-8762j: an epic's contract is checked when the turn proposes it, rides the stored proposal, is
/// re-keyed onto the minted ids at filing with the drafts' own map, and lands on the stored approved set.
/// </summary>
public sealed class SeriesContractFilingTests
{
    private const string Clean = """
        contract:
          names:
            - {name: SpecReviewRound, kind: event, number: 87, owner: p9001}
          areas:
            - {spec: p9001, paths: [src/A.cs]}
            - {spec: 2, paths: [src/A.cs]}
          budgets:
            - {path: src/A.cs, spec: p9001, max_lines: 100}
            - {path: src/A.cs, spec: p9002, max_lines: 110}
        """;

    private const string Colliding = """
        contract:
          names:
            - {name: SpecReviewRound, kind: event, number: 87, owner: p9001}
            - {name: DefaultDecisionTaken, kind: event, number: 87, owner: p9002}
          areas:
            - {spec: p9001, paths: [src/A.cs]}
            - {spec: p9002, paths: [src/A.cs]}
        """;

    [Fact]
    public void EpicOutcomeParser_CollidingContract_IsOutcomeInvalid()
    {
        var resolution = Parse(Colliding);

        var error = resolution.Should().BeOfType<OutcomeInvalid>().Subject.Error;
        error.Should().Contain("event 87 is given to 'SpecReviewRound' and 'DefaultDecisionTaken'")
            .And.Contain("src/A.cs is claimed by p9001, p9002 without a line budget for p9001, p9002");
    }

    [Fact]
    public void EpicOutcomeParser_Contract_RidesOnTheOutcome()
    {
        var epic = (EpicOutcome)Parse(Clean).Should().BeOfType<OutcomeResolved>().Subject.Proposal;

        epic.Contract!.Areas.Select(a => a.Spec).Should().Equal("p9001", "p9002");
        ((EpicOutcome)Parse(string.Empty).Should().BeOfType<OutcomeResolved>().Subject.Proposal).Contract.Should().BeNull();
    }

    [Fact]
    public void OutcomeProposalJson_EpicContract_RoundTrips()
    {
        var epic = new EpicOutcome(Draft("parent"), [Draft("c-1"), Draft("c-2")], Contract());

        var back = (EpicOutcome)OutcomeProposalJson.Read(OutcomeProposalJson.Write(epic));

        back.Contract!.Names.Should().ContainSingle().Which.Writers.Should().Equal("c-2");
        back.Contract.Budgets.Should().Equal(Contract().Budgets);
        OutcomeProposalJson.Write(epic with { Contract = null }).Should().NotContain("contract");
    }

    [Fact]
    public void FiledSeriesFactory_Mint_ContractFollowsTheReid()
    {
        var epic = new EpicOutcome(Draft("parent"), [Draft("c-1"), Draft("c-2")], Contract());

        var filed = ApprovedSetDoubles.SeriesFiling().Under("2026-10-14-0d0d", epic.Children, epic);

        filed.Drafts.Select(d => d.SpecId).Should().Equal("2026-10-14-0d0da", "2026-10-14-0d0db");
        filed.Contract!.Names[0].Owner.Should().Be("2026-10-14-0d0da");
        filed.Contract.Names[0].Writers.Should().Equal("2026-10-14-0d0db");
        filed.Contract.Budgets.Select(b => b.Spec).Should().Equal("2026-10-14-0d0da", "2026-10-14-0d0db");
        ApprovedSetDoubles.SeriesFiling().Under("2026-10-14-0d0d", epic.Children, new SpecOutcome(Draft("c-1")))
            .Contract.Should().BeNull("only an epic carries a contract");
    }

    [Fact]
    public void FiledSeriesFactory_FollowUpWithoutExecutedChildren_DropsTheirEntries()
    {
        var epic = new EpicOutcome(Draft("parent"), [Draft("c-1"), Draft("c-2")], Contract());
        var followUp = epic with { Children = [epic.Children[1]] };

        var filed = ApprovedSetDoubles.SeriesFiling().Under("2026-10-14-0d0d", followUp.Children, followUp);

        filed.Contract!.Names.Should().BeEmpty("its owner c-1 already ran on the closed ticket");
        filed.Contract.Budgets.Should().ContainSingle().Which.Spec.Should().Be("2026-10-14-0d0da");
    }

    [Fact]
    public void OutcomeTicketFiler_LoneSpec_PassesTheProposalAsSource()
    {
        var root = Root();
        var calls = new[] { "OutcomeTicketFiler.cs", "AmendedSpecification.cs" }
            .SelectMany(file => Regex.Matches(File.ReadAllText(Path.Combine(root, file)), @"\.(Mint|Under)\(([^;]*?)\)[,;]")
                .Select(m => m.Groups[2].Value)).ToList();

        calls.Should().HaveCount(4, "a lone spec and an epic, filed and amended");
        calls.Should().OnlyContain(args => Regex.IsMatch(args, @",\s*(proposal|spec|epic)\)?\s*$"),
            "every filing site hands the approved proposal on, so a source-carried field reaches a lone spec too");
    }

    [Fact]
    public async Task ApprovedSpecSetRecorder_EpicContract_IsOnTheStoredSet()
    {
        var store = ApprovedSetDoubles.Store();
        var series = ApprovedSetDoubles.Series(Draft("2026-10-06-0a0aa"), Draft("2026-10-06-0a0ab")) with
        {
            Contract = SeriesContractFixtures.Of(names: [new("SpecReviewRound", "event", 87, "2026-10-06-0a0aa")]),
        };

        var record = await ApprovedSetDoubles.Recorder(store).RecordAsync(State(), Project(), "19106", series, "goal", default);

        var stored = SpecApprovalJson.Read(SpecApprovalJson.Write(record))!;
        stored.Set.Contract!.Names.Should().ContainSingle().Which.Owner.Should().Be("2026-10-06-0a0aa");
        stored.Set.Goal.Should().Be("goal");
    }

    [Fact]
    public void ProposalReProposalGuard_ReplyChangesTheContract_KeepsTheLastValidOne()
    {
        var last = new EpicOutcome(Draft("parent"), [Draft("c-1"), Draft("c-2")], Contract());
        var reply = last with { Contract = SeriesContractFixtures.Of(names: [new("Other", "event", 1, "c-2")]) };

        var merged = new ProposalReProposalGuard(new SpecRevisionGuard()).Merge(
            last, reply, new Dictionary<string, ProposalDraftLicence>(), parentNamed: false, lastRound: false);

        ((EpicOutcome)merged!.Merged).Contract.Should().BeSameAs(last.Contract, "no draft's cut licenses the contract");
    }

    private static OutcomeResolution Parse(string contract)
    {
        var validator = new SpecDraftValidator(new SpecSchemaProvider());
        var parser = new EpicOutcomeParser(validator, new SpecDraftReader(), new RequiresEdgeChecker(),
            DialogTestRepos.Real(), SeriesContracts.Reader());
        return parser.Parse(OutcomeYamlReader.ReadMap($"""
            kind: epic
            parent:
              spec: p9000
              goal: "The sample migration"
              steps:
                - id: all
                  action: "Migrate the sample estate"
              done:
                - "The estate is migrated."
            children:
              - {Child("p9001")}
              - {Child("p9002")}
            {contract}
            """), []);
    }

    private static string Child(string id) =>
        $"{{spec: {id}, goal: \"Slice {id}\", steps: [{{id: s, action: \"Do {id}\"}}], done: [\"{id} is finished.\"]}}";

    private static SeriesContract Contract() => SeriesContractFixtures.Of(
        names: [new("SpecReviewRound", "event", 87, "c-1", ["c-2"])],
        areas: [new("c-1", ["src/A.cs"]), new("c-2", ["src/A.cs"])],
        budgets: [new("src/A.cs", "c-1", 100), new("src/A.cs", "c-2", 110)]);

    private static SpecDraft Draft(string id) =>
        new(id, $"Do {id}", $"spec: {id}\ngoal: \"Do {id}\"", []) { Done = ["It is done."] };

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "backend")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "backend", "AgentSmith.Server", "Services", "SpecDialog");
    }

    private static ConversationState State() => new()
    {
        JobId = "session-1", ChannelId = "channel", UserId = "sample.user", Platform = "dashboard",
        Project = "sample", TicketId = string.Empty, StartedAt = DateTimeOffset.UtcNow,
        Scope = new ActiveScope { Project = "sample", Repos = ["sample-api"] },
    };

    private static AgentSmith.Contracts.Models.Configuration.ResolvedProject Project() => new()
    {
        Name = "sample",
        Tracker = new AgentSmith.Contracts.Models.Configuration.TrackerConnection
            { Type = AgentSmith.Contracts.Models.Configuration.TrackerType.AzureDevOps },
        Repos = [new AgentSmith.Contracts.Models.Configuration.RepoConnection { Name = "sample-api" }],
    };
}
