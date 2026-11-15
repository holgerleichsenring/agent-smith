using System.Globalization;
using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-15-53d0: the operator's own messages in the design conversation the run's ticket was approved in, as the
/// approval record carries them — shown to the derivation and to every spec reviewer beside the uploaded material.
/// It says what they are and where they come from; how much they weigh is the reader's judgement. Empty with none.
/// </summary>
public static class ApprovalOwnerWordsSection
{
    public const string Heading = "## The operator's messages in the design conversation this work was approved in";

    public const string Description =
        "Each message as the operator wrote it, oldest first, with its author and time (UTC). The design partner's "
        + "replies are not shown.";

    /// <summary>The section over the record the run published, or empty when it carries no words.</summary>
    public static string For(PipelineContext pipeline) =>
        Render(ApprovedSpecSetResolver.PublishedIn(pipeline)?.OwnerWords ?? []);

    public static string Render(IReadOnlyList<OwnerWord> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (words.Count == 0) return string.Empty;
        var sb = new StringBuilder().AppendLine().AppendLine().AppendLine(Heading).AppendLine(Description);
        foreach (var word in words)
            sb.AppendLine().AppendLine($"### {word.Author.Trim()}, "
                + word.At.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC")
                .AppendLine(word.Text.Trim());
        return sb.ToString();
    }
}
