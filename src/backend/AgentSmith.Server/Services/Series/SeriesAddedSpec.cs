using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-27-3532a: the spec the series master's add_ticket files — validated against the spec schema and read as a draft, then
/// filed as a spec under the series' plan approval (its conversation and approver), so its ticket gets an approval record and its
/// set on its branch, and its run carries it. Its scope repositories are the spec's <c>repos:</c>; the spec's own <c>requires:</c>
/// is dropped from the draft and from the stored yaml, since the ticket's edges come from the tool's arguments. It is filed
/// unstarted: its master starts it.
/// </summary>
public sealed class SeriesAddedSpec(ISpecDraftValidator validator, SpecDraftReader reader)
{
    /// <summary>The filing request, or the schema's error when the yaml is no valid spec (then nothing is filed).</summary>
    public (FilingRequest? Request, string? Error) Request(SeriesRunRecord series, string spec)
    {
        ArgumentNullException.ThrowIfNull(series);
        var outcome = validator.ValidateYaml(WithoutRequires(spec ?? string.Empty));
        if (outcome is SpecDraftInvalid invalid) return (null, invalid.Error);
        if (outcome is not SpecDraftValid valid) return (null, "the spec is empty");
        var draft = reader.Read(valid.Yaml);
        return (new FilingRequest(series.Conversation, series.Principal, draft.Repos, [], new SpecOutcome(draft), MayStartRuns: false)
        {
            IsSeriesMasterAddition = true,
        }, null);
    }

    // The top-level requires: key with its block (indented or list lines), whatever form it was written in.
    private static string WithoutRequires(string yaml)
    {
        var kept = new List<string>();
        var inRequires = false;
        foreach (var line in yaml.Replace("\r\n", "\n").Split('\n'))
        {
            var topLevel = line.Length > 0 && !char.IsWhiteSpace(line[0]) && !line.StartsWith('-') && !line.StartsWith('#');
            if (topLevel) inRequires = line.StartsWith("requires:", StringComparison.Ordinal);
            if (!inRequires) kept.Add(line);
        }
        return string.Join('\n', kept);
    }
}
