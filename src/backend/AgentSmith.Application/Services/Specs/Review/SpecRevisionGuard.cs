using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762f: proves a reviser changed only what a round's cuts name. Both documents are compared
/// PARSED, so folding, quoting, key order and comments never decide. Goal and scope are judged as one item
/// each, the list registers by <see cref="SpecRevisionLists"/>, assumptions only grow by a fact moved on the
/// last round, and every other key — spec, requires, applies_to, repos, contexts — must be equal. Any
/// change beyond that refuses the whole revision: a partial apply is a spec nobody wrote.
/// </summary>
public sealed class SpecRevisionGuard
{
    private const string Assumptions = "assumptions";

    private static readonly string[] Lists =
    [
        SpecRegisterNames.Decisions, SpecRegisterNames.Steps, SpecRegisterNames.Tests,
        SpecRegisterNames.Done, SpecRegisterNames.Facts,
    ];

    /// <summary>Null when the revision is admitted, otherwise why it is refused.</summary>
    public string? Check(
        string originalYaml, string revisedYaml, IReadOnlyList<SpecReviewCut> cuts,
        IReadOnlyList<SpecEvidenceProblem> problems, bool lastRound)
    {
        ArgumentNullException.ThrowIfNull(cuts);
        ArgumentNullException.ThrowIfNull(problems);
        IReadOnlyDictionary<string, object?> original, revised;
        try
        {
            original = OutcomeYamlReader.ReadMap(originalYaml);
            revised = OutcomeYamlReader.ReadMap(revisedYaml);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return "the revised spec does not read as a mapping";
        }

        var registered = SpecRegisterNames.All.Append(Assumptions).ToHashSet(StringComparer.Ordinal);
        var changedKey = original.Keys.Union(revised.Keys).Where(key => !registered.Contains(key))
            .FirstOrDefault(key => !SpecRevisionEdit.Equal(original.GetValueOrDefault(key), revised.GetValueOrDefault(key)));
        if (changedKey is not null) return $"the revision changed '{changedKey}', which no cut may touch";

        foreach (var single in new[] { SpecRegisterNames.Goal, SpecRegisterNames.Scope })
            if (!SpecRevisionEdit.IsItemEdit(original.GetValueOrDefault(single), revised.GetValueOrDefault(single), cuts))
                return $"the {single} was changed beyond its cuts";

        var lists = new SpecRevisionLists(cuts, problems, lastRound);
        foreach (var register in Lists)
            if (lists.Judge(register, ItemsOf(original, register), ItemsOf(revised, register)) is { } refusal)
                return refusal;
        return lists.JudgeAssumptions(ItemsOf(original, Assumptions), ItemsOf(revised, Assumptions));
    }

    private static List<object?> ItemsOf(IReadOnlyDictionary<string, object?> map, string key) =>
        map.GetValueOrDefault(key) switch
        {
            List<object?> list => list,
            null => [],
            var single => [single],
        };
}
