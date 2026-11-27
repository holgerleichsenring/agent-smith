using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>
/// 2026-11-27-3532d: the coding master found the spec it builds cannot be implemented and retires it for the spec it
/// wrote instead. The retired spec stays in the set as it was written, its record kept; the replacement gets the series'
/// next free member id — minted here, never by the model — names the retired spec in <c>supersedes</c>, takes its place,
/// label, companion and carried segments, and every later spec that required the retired one requires the replacement.
/// A spec that already ran is never retired: a correction to executed work is a new spec.
/// </summary>
public sealed class SpecRetirement(ISpecDraftValidator validator, SpecDraftReader reader, SpecDraftIdRewriter rewriter)
{
    /// <returns>The set as the retirement leaves it and the replacement, or why the replacement cannot stand.</returns>
    public (SpecSet? Set, SpecDraft? Replacement, string? Refusal) Retire(PipelineContext pipeline, SpecDraft current, string? yaml)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(current);
        var set = pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        if (set.Executed.Contains(current.SpecId, StringComparer.Ordinal))
            return Refused($"spec {current.SpecId} already ran, so it is never retired; a correction to it is a new spec");
        if (string.IsNullOrWhiteSpace(set.Series))
            return Refused($"spec set {set.Key} names no series to give the replacement an id under");
        var (read, invalid) = Read(yaml);
        if (read is null) return Refused(invalid!);
        if (SeriesIdFactory.FreeWithin(set.Series, SeriesConfig.MemberLetters, Taken(pipeline, set)) is not { } id)
            return Refused($"series {set.Series} has used its {SeriesConfig.MemberLetters} member letters (a..z), so no id is left for a replacement");
        if (EdgeRefusal(set, current, read) is { } edge) return Refused(edge);
        var repos = read.Repos.Count > 0 ? read.Repos : current.Repos;
        if (repos.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            return Refused("a spec is written into exactly one repository; name one in repos");
        var text = SpecSupersedesLine.Stamp(rewriter.Rewrite(read.Yaml, new Dictionary<string, string> { [read.SpecId] = id }), current.SpecId);
        var (stamped, broken) = Read(text);
        if (stamped is null) return Refused(broken!);
        var replacement = stamped with { SpecId = id, Repos = repos };
        return (Replaced(set, current.SpecId, replacement), replacement, null);
    }

    private static (SpecSet?, SpecDraft?, string?) Refused(string why) => (null, null, why);

    private (SpecDraft? Draft, string? Refusal) Read(string? yaml)
    {
        var text = yaml ?? string.Empty;
        var outcome = text.Contains("```", StringComparison.Ordinal) ? validator.Validate(text) : validator.ValidateYaml(text);
        return outcome is SpecDraftValid valid ? (reader.Read(valid.Yaml), null)
            : (null, outcome is SpecDraftInvalid invalid ? $"the replacement is invalid: {invalid.Error}" : "spec_yaml holds no spec");
    }

    // Every id the series' members, its executed specs and the branch's done/ files hold.
    private static IReadOnlyCollection<string> Taken(PipelineContext pipeline, SpecSet set)
    {
        var taken = new HashSet<string>(set.Specs.Select(s => s.SpecId).Concat(set.Executed), StringComparer.Ordinal);
        if (pipeline.TryGet<IReadOnlyList<string>>(ContextKeys.SpecTakenIds, out var branch) && branch is not null) taken.UnionWith(branch);
        return taken;
    }

    // The replacement stands where the retired spec stood, so it requires only what comes before it.
    private static string? EdgeRefusal(SpecSet set, SpecDraft current, SpecDraft read)
    {
        if (read.Requires.Contains(current.SpecId, StringComparer.Ordinal))
            return $"the replacement takes {current.SpecId}'s place, so it cannot require it; name what {current.SpecId} required instead";
        var later = set.Specs.SkipWhile(s => s.SpecId != current.SpecId).Skip(1).Select(s => s.SpecId).ToHashSet(StringComparer.Ordinal);
        return read.Requires.FirstOrDefault(later.Contains) is { } after
            ? $"the replacement requires {after}, which comes after it in the series"
            : null;
    }

    private SpecSet Replaced(SpecSet set, string retiredId, SpecDraft replacement)
    {
        var relink = new Dictionary<string, string>(StringComparer.Ordinal) { [retiredId] = replacement.SpecId };
        var specs = new List<DerivedSpec>(set.Specs.Count + 1);
        var passed = false;
        foreach (var spec in set.Specs)
        {
            if (spec.SpecId == retiredId)
            {
                specs.Add(spec);
                specs.Add(new DerivedSpec(replacement, spec.Slug, spec.Markdown.Replace(retiredId, replacement.SpecId, StringComparison.Ordinal),
                    spec.CarriedSegments));
                passed = true;
                continue;
            }
            specs.Add(passed && !set.Executed.Contains(spec.SpecId, StringComparer.Ordinal)
                && spec.Draft.Requires.Contains(retiredId, StringComparer.Ordinal) ? Relinked(spec, relink) : spec);
        }
        return set with
        {
            Specs = specs,
            StandIns = [.. set.StandIns.Where(id => id != retiredId)],
            Accounting = set.Accounting with
            {
                Carried = [.. set.Accounting.Carried.Select(c => c.SpecId == retiredId ? c with { SpecId = replacement.SpecId } : c)],
            },
        };
    }

    private DerivedSpec Relinked(DerivedSpec spec, IReadOnlyDictionary<string, string> relink) => spec with
    {
        Draft = spec.Draft with
        {
            Requires = [.. spec.Draft.Requires.Select(r => rewriter.RewriteValue(r, relink))],
            Yaml = rewriter.Rewrite(spec.Draft.Yaml, relink),
        },
    };
}
