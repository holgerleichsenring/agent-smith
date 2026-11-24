using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-24-f9c5d: what a GitHub Actions workflow file's <c>on:</c> says about pull requests — whether a pull_request or
/// pull_request_target event runs it for a base branch (its branches / branches-ignore filters, GitHub's glob: <c>*</c>
/// within a segment, <c>**</c> across, a leading <c>!</c> excluding again) — and whether it declares workflow_dispatch.
/// Configuration read, not code: the workflow's jobs and their conditions are not looked at.
/// </summary>
public sealed record GitHubWorkflowTriggers(bool OnPullRequest, IReadOnlyList<string> Branches, IReadOnlyList<string> Ignored, bool Dispatch)
{
    public static readonly GitHubWorkflowTriggers None = new(false, [], [], false);

    /// <summary>Whether a pull request into <paramref name="baseBranch"/> runs the workflow.</summary>
    public bool RunsFor(string baseBranch)
    {
        if (!OnPullRequest) return false;
        if (Branches.Count > 0) return Filtered(Branches, baseBranch);
        return Ignored.Count == 0 || !Filtered(Ignored, baseBranch);
    }

    public static GitHubWorkflowTriggers Parse(string yaml)
    {
        var stream = new YamlStream();
        try { stream.Load(new StringReader(yaml)); }
        catch (YamlDotNet.Core.YamlException) { return None; }
        if (stream.Documents.FirstOrDefault()?.RootNode is not YamlMappingNode root
            || root.Children.FirstOrDefault(c => c.Key is YamlScalarNode { Value: "on" or "true" }).Value is not { } on) return None;
        return on switch
        {
            YamlScalarNode scalar => Of([scalar.Value ?? string.Empty], null),
            YamlSequenceNode list => Of([.. list.Children.OfType<YamlScalarNode>().Select(n => n.Value ?? string.Empty)], null),
            YamlMappingNode map => Of([.. map.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value ?? string.Empty)], map),
            _ => None,
        };
    }

    private static GitHubWorkflowTriggers Of(IReadOnlyList<string> events, YamlMappingNode? map)
    {
        var pull = events.FirstOrDefault(e => e is "pull_request" or "pull_request_target");
        var filter = pull is not null && map?.Children.FirstOrDefault(c => c.Key is YamlScalarNode { Value: var v } && v == pull).Value
            is YamlMappingNode f ? f : null;
        return new(pull is not null, Strings(filter, "branches"), Strings(filter, "branches-ignore"), events.Contains("workflow_dispatch"));
    }

    private static IReadOnlyList<string> Strings(YamlMappingNode? map, string key) =>
        map?.Children.FirstOrDefault(c => c.Key is YamlScalarNode { Value: var v } && v == key).Value switch
        {
            YamlSequenceNode list => [.. list.Children.OfType<YamlScalarNode>().Select(n => n.Value ?? string.Empty)],
            YamlScalarNode one => [one.Value ?? string.Empty],
            _ => [],
        };

    // GitHub's rule: the patterns in order, a later !pattern excluding what an earlier one matched.
    private static bool Filtered(IReadOnlyList<string> patterns, string branch)
    {
        var matched = false;
        foreach (var pattern in patterns)
            if (pattern.StartsWith('!')) matched &= !Glob(pattern[1..], branch);
            else matched |= Glob(pattern, branch);
        return matched;
    }

    private static bool Glob(string pattern, string branch) =>
        Regex.IsMatch(branch, "^" + Regex.Escape(pattern).Replace(@"\*\*", "\u0001").Replace(@"\*", "[^/]*").Replace("\u0001", ".*") + "$");
}
