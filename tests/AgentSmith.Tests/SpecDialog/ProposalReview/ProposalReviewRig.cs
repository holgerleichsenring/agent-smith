using System.Collections.Concurrent;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Turns;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.Specs;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using YamlDotNet.Serialization;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-10-13-8762i: a design turn's proposal fixtures — drafts, epics, ticket lists — and 2026-11-18-9c1ca the
/// mechanical checks the turn runs on its draft.
/// </summary>
internal sealed class ProposalReviewRig
{
    public const string Parent = "2026-10-14-9000a";
    public const string First = "2026-10-14-9000b";
    public const string Second = "2026-10-14-9000c";
    public const string Lone = "2026-10-14-7000a";
    public const string Done = "Every order of the day lands in the export file.";

    public PipelineContext Pipeline { get; } = new();

    /// <summary>2026-11-11-2bdfd: the turn's reply slot, seeded as the pipeline seeds it; a test writes its
    /// Questions as state_open_questions would.</summary>
    public SpecDialogReplySlot Slot { get; } = new();

    public ProposalReviewRig() => Pipeline.Set(ContextKeys.SpecDialogReplySlot, Slot);

    public static OpenQuestion Question(string question, string proposal, params string[] specIds) =>
        new(question, proposal, specIds);
    public static SpecReviewCut Cut(string register, string quote, string? replacement) =>
        new(register, quote, replacement, "shop/src/Export/Writer.cs:9 [A1]");

    public static string Yaml(string id, string goal = "The export writes every order of the day into one file.",
        string step = "Write the orders through the buffered file writer.", params string[] requires) => $$"""
        spec: {{id}}
        goal: "{{goal}}"
        applies_to: "backend (export)"
        requires: [{{string.Join(", ", requires)}}]
        scope:
          in: "The export handler and its file writer."
          out: "The import side stays as it is."
        steps:
          - id: writer
            action: "{{step}}"
        tests:
          - "Export_WritesEveryOrderOfTheDay"
        done:
          - "{{Done}}"
        facts:
          - claim: "The export handler reads the orders from the order store."
            evidence: "src/Export/ExportHandler.cs:12"
        """;

    /// <summary>2026-11-17-5701a: a list of two tickets, each carrying one spec; order-api's spec takes <paramref name="step"/>.</summary>
    public static TicketGraphOutcome TicketSpecs(string step = "Write the orders through the buffered file writer.") => new(
    [
        Validation.TicketGraphFixtures.Ticket("order-api") with { Specs = [new SpecDraftReader().Read(Yaml("order-state", step: step))] },
        Validation.TicketGraphFixtures.Ticket("order-ui", "order-api") with { Specs = [new SpecDraftReader().Read(Yaml("order-page"))] },
    ]);

    public static string SpecReply(string yaml, string prose = "Here is the spec.") => $"{prose}\n\n```yaml\n{yaml}\n```\n";

    public static string EpicReply(
        string parent, IEnumerable<string> children, string prose = "Here is the epic.", object? contract = null)
    {
        var map = new Dictionary<object, object?>
        {
            ["kind"] = "epic",
            ["parent"] = Map(parent),
            ["children"] = children.Select(Map).ToList(),
        };
        if (contract is not null) map["contract"] = contract;
        return $"{prose}\n\n```outcome\n{OutcomeYamlReader.ToYaml(map)}```\n";
    }

    public static string Epic(string? first = null, string? second = null, string? parent = null) =>
        EpicReply(parent ?? Yaml(Parent, "The export is rebuilt in two slices."),
            [first ?? Yaml(First), second ?? Yaml(Second, requires: [First])]);

    /// <summary>2026-10-13-8762k: the two-child epic with a series contract — event 87 owned by the first child.</summary>
    public static string ContractEpic(string? second = null) => EpicReply(Yaml(Parent, "The export is rebuilt in two slices."),
        [Yaml(First), second ?? Yaml(Second, requires: [First])], contract: new Dictionary<object, object?>
        {
            ["names"] = new List<object?>
            {
                new Dictionary<object, object?> { ["name"] = "OrderExported", ["kind"] = "event", ["number"] = 87, ["owner"] = First },
            },
        });

    /// <summary>2026-11-10-18cf: the turn holds a repository, so a reviewer could have read it.</summary>
    public ProposalReviewRig WithRepository()
    {
        Pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)new Dictionary<string, ISandbox>
        {
            ["shop"] = new EvidenceTreeSandbox(new Dictionary<string, string>()),
        });
        return this;
    }

    /// <summary>The proposal the gate admitted from <paramref name="reply"/>, published as the turn's outcome.</summary>
    public OutcomeProposal Propose(string reply)
    {
        var proposal = ((OutcomeResolved)MasterHandlerFixture.BuildOutcomeResolver().Resolve(reply, [])).Proposal;
        Pipeline.Set(ContextKeys.SpecDialogOutcome, proposal);
        return proposal;
    }

    /// <summary>2026-11-18-9c1ca: the mechanical checks a design turn's draft gets, over <paramref name="existence"/>.</summary>
    public static ProposalMechanicalChecks Checks(ITicketExistence? existence = null) =>
        new(ProposalEvidenceTestReview.Create(),
            new TicketExistenceFindings(NullLogger<TicketExistenceFindings>.Instance, existence),
            NullLogger<ProposalMechanicalChecks>.Instance);

    public OutcomeProposal Outcome => Pipeline.Get<OutcomeProposal>(ContextKeys.SpecDialogOutcome);

    public SpecDraft DraftOf(string specId) => ProposalSpecSet.Drafts(Outcome).Single(d => d.SpecId == specId);

    public static AgenticLoopResult Result(string text) =>
        new(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)), TimeSpan.Zero);

    private static object? Map(string yaml) => new DeserializerBuilder().Build().Deserialize<object?>(yaml);
}
