using System.Text;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762k: the members as <see cref="SeriesContractPrompt"/> renders them within its budget. Every member
/// starts in part; members are then shown whole, in order, while the text stays within the budget. What is still
/// in part is named, and a text past the budget even in part is cut off and says so.
/// </summary>
internal static class SeriesContractMembers
{
    private const string CutOff = "\n(cut off here — judge no member you were not shown)\n";

    public static string Render(SeriesContractInput input, int budget)
    {
        var terms = Terms(input.Contract);
        var parts = input.Members.Select(m => Part(m, terms)).ToList();
        var whole = new bool[parts.Count];
        var total = parts.Sum(p => p.Partial.Length) + 80 + (20 * parts.Count);
        for (var i = 0; i < parts.Count; i++)
        {
            var extra = parts[i].Whole.Length - parts[i].Partial.Length;
            if (total + extra > budget) continue;
            whole[i] = true;
            total += extra;
        }
        var inPart = input.Members.Where((_, i) => !whole[i]).Select(m => m.Draft.SpecId).ToList();
        var text = (inPart.Count == 0 ? string.Empty
                : $"Shown in part (goal, scope, steps and the lines naming the contract): {string.Join(", ", inPart)}.\n\n")
            + string.Join("\n", parts.Select((p, i) => whole[i] ? p.Whole : p.Partial));
        return text.Length <= budget ? text : text[..Math.Max(0, budget - CutOff.Length)] + CutOff;
    }

    private static (string Whole, string Partial) Part(SeriesContractMember member, IReadOnlyList<string> terms)
    {
        var heading = $"### {member.Draft.SpecId}{(member.Status is null ? string.Empty : $" ({member.Status})")}\n";
        var registers = SpecRegisters.Read(member.Draft.Yaml);
        var sb = new StringBuilder(heading);
        foreach (var register in new[] { SpecRegisterNames.Goal, SpecRegisterNames.Scope, SpecRegisterNames.Steps })
            foreach (var entry in registers.Entries(register)) sb.AppendLine($"{register}: {entry}");
        foreach (var register in new[] { SpecRegisterNames.Facts, SpecRegisterNames.Done })
            foreach (var entry in registers.Entries(register).Where(e => terms.Any(t => e.Contains(t, StringComparison.OrdinalIgnoreCase))))
                sb.AppendLine($"{register}: {entry}");
        return (heading + member.Draft.Yaml.TrimEnd() + "\n", sb.ToString());
    }

    private static IReadOnlyList<string> Terms(SeriesContract contract) =>
    [
        .. new[]
            {
                contract.Names.Select(n => n.Name), contract.Data.SelectMany(d => new[] { d.Datum, d.LivesIn }),
                contract.Areas.SelectMany(a => a.Paths), contract.Budgets.Select(b => b.Path),
            }
            .SelectMany(t => t).Where(t => !string.IsNullOrWhiteSpace(t)),
    ];
}
