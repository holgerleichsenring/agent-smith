using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Tests.Validation;

/// <summary>2026-10-14-dbcej: ticket texts and blocks for the list-of-tickets outcome tests.</summary>
internal static class TicketGraphFixtures
{
    internal static readonly IReadOnlyList<string> RepoScope = ["api", "web"];

    internal static GraphTicket Ticket(string key, params string[] requires) => new(
        key, $"Title of {key}", $"The scope of {key} in prose.", [$"{key} is done"], requires, []);

    internal static TicketGraphChecker Checker() => new(new TicketGraphOrderer());

    internal static TicketGraphOutcomeParser Parser() => new(Checker());

    /// <summary>A ```outcome block of kind tickets around the given YAML list items.</summary>
    internal static string Block(string tickets) => $"Here is the list.\n\n```outcome\nkind: tickets\ntickets:\n{tickets}```\n";

    /// <summary>One ticket as YAML list item lines, every string quoted.</summary>
    internal static string Item(string key, string requires = "[]", string? extra = null) =>
        $"  - key: \"{key}\"\n    title: \"Title of {key}\"\n    requirement: \"The scope of {key}.\"\n"
        + $"    done: [\"{key} is done\"]\n    requires: {requires}\n{extra}";

    internal static OutcomeResolution Parse(string tickets) =>
        Parser().Parse(OutcomeYamlReader.ReadMap($"kind: tickets\ntickets:\n{tickets}"), RepoScope);
}
