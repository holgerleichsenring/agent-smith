using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: the name a new PoC is proposed under, from the uploaded ZIP's file name — lower-cased, every run
/// of characters outside [a-z0-9-] folded to one '-', and trimmed to a DNS label of at most 40 characters, cut at a
/// word boundary when one is in reach. The person may edit it; <see cref="IsName"/> is what the import accepts.
/// </summary>
public static partial class PocNameProposal
{
    public const int MaxNameChars = 40; // not a limit: keeps the namespace poc-<name> of 933dg's PocNamespace and the names derived from it inside the 63-character DNS label, RFC 1035

    /// <summary>The name proposed when the file name holds no letter or digit.</summary>
    public const string Fallback = "app";

    /// <summary>The proposal for <paramref name="fileName"/>, e.g. 'My Cool App (2).zip' gives 'my-cool-app-2'.</summary>
    public static string From(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var name = fileName.Replace('\\', '/').Split('/')[^1];
        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        var folded = Folded().Replace(name.ToLowerInvariant(), "-").Trim('-');
        if (folded.Length > MaxNameChars)
        {
            var cut = folded.LastIndexOf('-', MaxNameChars);
            folded = (cut > 0 ? folded[..cut] : folded[..MaxNameChars]).Trim('-');
        }
        return folded.Length == 0 ? Fallback : folded;
    }

    /// <summary>Whether <paramref name="name"/> is a PoC name: a DNS label of at most 40 characters.</summary>
    public static bool IsName(string? name) =>
        name is { Length: > 0 and <= MaxNameChars } && Label().IsMatch(name);

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex Folded();

    [GeneratedRegex("^[a-z0-9]([-a-z0-9]*[a-z0-9])?$")]
    private static partial Regex Label();
}
