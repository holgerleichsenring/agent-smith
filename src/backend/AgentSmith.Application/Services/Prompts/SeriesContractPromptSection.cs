using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-10-13-8762j: a series contract as a prompt reads it — its names, data, areas and budgets, each member by
/// spec id. Empty for no contract. The re-cut renders it so the model repeats it; the set-scope review reuses it,
/// and 2026-10-13-8762k's reviewers quote its <see cref="Lines"/> as the entry a finding breaks.
/// </summary>
public static class SeriesContractPromptSection
{
    public const string Heading = "## The series contract";

    public static string Render(SeriesContract? contract)
    {
        if (contract is null || contract.IsEmpty) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine(Heading);
        foreach (var entry in Lines(contract)) sb.AppendLine($"- {entry.Text}");
        return sb.ToString();
    }

    /// <summary>Every entry as one line, with the members it names (owner, writers or the member itself).</summary>
    public static IReadOnlyList<SeriesContractLine> Lines(SeriesContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return
        [
            .. contract.Names.Select(name => new SeriesContractLine(
                $"name: {name.Kind} '{name.Name}'{(name.Number is { } n ? $" = {n}" : string.Empty)}, "
                + $"owner {name.Owner}{Writers(name.Writers)}", [name.Owner, .. name.Writers ?? []])),
            .. contract.Data.Select(datum => new SeriesContractLine(
                $"datum: '{datum.Datum}' lives in {datum.LivesIn}, owner {datum.Owner}{Writers(datum.Writers)}",
                [datum.Owner, .. datum.Writers ?? []])),
            .. contract.Areas.Select(area => new SeriesContractLine(
                $"area of {area.Spec}: {string.Join(", ", area.Paths)}", [area.Spec])),
            .. contract.Budgets.Select(budget => new SeriesContractLine(
                $"budget: {budget.Path} at most {budget.MaxLines} lines once {budget.Spec} is built", [budget.Spec])),
        ];
    }

    private static string Writers(IReadOnlyList<string>? writers) =>
        writers is { Count: > 0 } ? $", writers {string.Join(", ", writers)}" : string.Empty;
}

/// <summary>2026-10-13-8762k: one rendered contract entry and the members it names.</summary>
public sealed record SeriesContractLine(string Text, IReadOnlyList<string> Members);
