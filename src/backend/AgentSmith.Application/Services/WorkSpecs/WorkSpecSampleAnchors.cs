using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: the anchors spec.md offers. One rule, one home — the RULE lives in
/// spec.yaml and its SAMPLE lives under a <c>## sample:&lt;anchor&gt;</c> heading in
/// spec.md, referenced by anchor. Neither file is generated from the other, so
/// the anchor set is the only coupling between them and it is checked.
/// </summary>
public static partial class WorkSpecSampleAnchors
{
    /// <summary>Heading prefix that marks a sample block in spec.md.</summary>
    public const string HeadingPrefix = "sample:";

    public static IReadOnlySet<string> Parse(string? samplesMarkdown)
    {
        var anchors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(samplesMarkdown)) return anchors;
        foreach (Match match in HeadingPattern().Matches(samplesMarkdown))
            anchors.Add(match.Groups["anchor"].Value.Trim());
        return anchors;
    }

    [GeneratedRegex(@"^#{1,6}\s+sample:(?<anchor>[^\r\n]+?)\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex HeadingPattern();
}
