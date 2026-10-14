using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-10-13-8762j: a series contract as a prompt reads it — its names, data, areas and budgets, each member by
/// spec id. Empty for no contract. The re-cut renders it so the model repeats it; the set-scope review reuses it.
/// </summary>
public static class SeriesContractPromptSection
{
    public static string Render(SeriesContract? contract)
    {
        if (contract is null || contract.IsEmpty) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("## The series contract");
        foreach (var name in contract.Names)
            sb.AppendLine($"- name: {name.Kind} '{name.Name}'{(name.Number is { } n ? $" = {n}" : string.Empty)}, "
                + $"owner {name.Owner}{Writers(name.Writers)}");
        foreach (var datum in contract.Data)
            sb.AppendLine($"- datum: '{datum.Datum}' lives in {datum.LivesIn}, owner {datum.Owner}{Writers(datum.Writers)}");
        foreach (var area in contract.Areas)
            sb.AppendLine($"- area of {area.Spec}: {string.Join(", ", area.Paths)}");
        foreach (var budget in contract.Budgets)
            sb.AppendLine($"- budget: {budget.Path} at most {budget.MaxLines} lines once {budget.Spec} is built");
        return sb.ToString();
    }

    private static string Writers(IReadOnlyList<string>? writers) =>
        writers is { Count: > 0 } ? $", writers {string.Join(", ", writers)}" : string.Empty;
}
