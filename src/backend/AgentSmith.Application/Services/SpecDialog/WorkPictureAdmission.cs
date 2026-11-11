using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-05-862ea: whether a stated work picture is stored. Kind and size are closed sets, read case-insensitively
/// with or without dashes; truth is one line of plain prose within <see cref="WorkPictureLimits.Truth"/>. A refusal
/// names the rule broken, so the model can state the picture again.
/// </summary>
public static class WorkPictureAdmission
{
    /// <summary>Marks that make a line markup rather than prose, wherever they stand.</summary>
    private const string Markup = "`*#|";

    /// <summary>The picture as it would be stored, or the rule it broke.</summary>
    /// <param name="effort">2026-11-10-548f: optional; a cost-cap tier other than unknown.</param>
    public static (WorkPicture? Picture, string? Refusal) Of(string? kind, string? truth, string? size, string? effort = null)
    {
        var line = truth?.Trim() ?? string.Empty;
        if (Parsed<UndertakingKind>(kind) is not { } k)
            return (null, $"The kind '{kind}' is none of: {Names<UndertakingKind>()}.");
        if (Parsed<UndertakingSize>(size) is not { } s)
            return (null, $"The size '{size}' is none of: {Names<UndertakingSize>()}.");
        var e = ComplexityTier.Unknown;
        if (!string.IsNullOrWhiteSpace(effort)
            && (Parsed<ComplexityTier>(effort) is not { } tier || tier == ComplexityTier.Unknown))
            return (null, $"The effort '{effort}' is none of: {Efforts}.");
        else if (!string.IsNullOrWhiteSpace(effort)) e = Parsed<ComplexityTier>(effort)!.Value;
        if (TruthRuleBroken(line) is { } rule) return (null, rule);
        // 2026-11-11-205fc: a series or a plan is reviewed draft by draft, so it says what its review may spend.
        return s != UndertakingSize.OneSpec && e == ComplexityTier.Unknown
            ? (null, $"A {WorkPictureText.Dashed(s.ToString())} names its effort, one of: {Efforts}.")
            : (new WorkPicture(k, line, s, e), null);
    }

    private static string Efforts => string.Join(", ",
        Enum.GetValues<ComplexityTier>().Where(t => t != ComplexityTier.Unknown).Select(t => t.ToString().ToLowerInvariant()));

    private static string? TruthRuleBroken(string line)
    {
        if (line.Length == 0) return "The truth is empty.";
        if (line.Length > WorkPictureLimits.Truth) return $"The truth is longer than {WorkPictureLimits.Truth} characters.";
        if (line.Any(char.IsControl)) return "The truth is more than one line.";
        return line.Any(Markup.Contains) ? $"The truth contains markup ({Markup})." : null;
    }

    // By name only: Enum.TryParse would also admit "1", which names no undertaking.
    private static T? Parsed<T>(string? value) where T : struct, Enum
    {
        var bare = (value ?? string.Empty).Replace("-", string.Empty, StringComparison.Ordinal).Trim();
        return Enum.GetValues<T>().Cast<T?>()
            .FirstOrDefault(v => string.Equals(v.ToString(), bare, StringComparison.OrdinalIgnoreCase));
    }

    private static string Names<T>() where T : struct, Enum =>
        string.Join(", ", Enum.GetValues<T>().Select(v => WorkPictureText.Dashed(v.ToString())));
}
