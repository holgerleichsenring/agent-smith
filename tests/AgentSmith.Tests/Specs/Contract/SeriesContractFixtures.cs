using AgentSmith.Contracts.Specs;

namespace AgentSmith.Tests.Specs.Contract;

/// <summary>2026-10-13-8762j: contracts and member lists the contract tests build from.</summary>
internal static class SeriesContractFixtures
{
    public const string Base = "2026-10-13-8762";

    public static string Id(char letter) => Base + letter;

    public static IReadOnlyList<(string Id, IReadOnlyList<string> Repos)> Members(string letters, params string[] repos) =>
        [.. letters.Select(l => (Id(l), (IReadOnlyList<string>)repos))];

    public static SeriesContract Of(
        IReadOnlyList<SeriesContractName>? names = null, IReadOnlyList<SeriesContractDatum>? data = null,
        IReadOnlyList<SeriesContractArea>? areas = null, IReadOnlyList<SeriesContractBudget>? budgets = null) =>
        new(names ?? [], data ?? [], areas ?? [], budgets ?? []);

    /// <summary>This series' own contract (series-contract-8762.md §2): the two events, the two tables, the
    /// series config section, and the EventType.cs and MasterToolComposition.cs budgets per member.</summary>
    public static SeriesContract Series8762() => Of(
        names:
        [
            new("SpecReviewRound", "event", 87, Id('e'), [Id('g'), Id('i'), Id('k')]),
            new("DefaultDecisionTaken", "event", 88, Id('o')),
            new("RunSpecReviewRounds", "table", null, Id('e')),
            new("RunDefaultDecisions", "table", null, Id('o'), [Id('p')]),
            new("series", "config", null, Id('u'), [Id('v')]),
            new("spec_review", "config", null, Id('g')),
            new("SpecSequenceStop", "context-key", null, Id('g'), [Id('h')]),
            new("run_gate_stage", "tool", null, Id('c')),
            new("TicketActivityChanged", "system-event", 56, Id('p'), [Id('v')]),
        ],
        data:
        [
            new("run-time review rounds", "RunSpecReviewRounds", Id('e'), [Id('g'), Id('k')]),
            new("default and its verdict", "RunDefaultDecisions", Id('o'), [Id('p')]),
            new("series contract", "the set manifest's contract:", Id('j'), [Id('k')]),
        ],
        areas:
        [
            new(Id('c'), ["src/backend/AgentSmith.Application/Services/Handlers/MasterToolComposition.cs"]),
            new(Id('d'), ["src/backend/AgentSmith.Application/Services/Handlers/MasterToolComposition.cs", "src/backend/AgentSmith.Application/Services/Loop/Sanity/"]),
            new(Id('m'), ["src/backend/AgentSmith.Application/Services/Handlers/MasterToolComposition.cs"]),
            new(Id('o'), ["src/backend/AgentSmith.Application/Services/Handlers/MasterToolComposition.cs", "src/backend/AgentSmith.Contracts/Events/EventType.cs"]),
            new(Id('e'), ["src/backend/AgentSmith.Contracts/Events/EventType.cs", "src/backend/AgentSmith.Application/Services/Specs/Review/"]),
            new(Id('j'), ["src/backend/AgentSmith.Application/Services/Specs/SeriesManifest.cs"]),
        ],
        budgets:
        [
            new("src/backend/AgentSmith.Application/Services/Handlers/MasterToolComposition.cs", Id('c'), 114),
            new("src/backend/AgentSmith.Application/Services/Handlers/MasterToolComposition.cs", Id('d'), 116),
            new("src/backend/AgentSmith.Application/Services/Handlers/MasterToolComposition.cs", Id('m'), 118),
            new("src/backend/AgentSmith.Application/Services/Handlers/MasterToolComposition.cs", Id('o'), 120),
            new("src/backend/AgentSmith.Contracts/Events/EventType.cs", Id('e'), 119),
            new("src/backend/AgentSmith.Contracts/Events/EventType.cs", Id('o'), 120),
        ]);
}
