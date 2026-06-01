using System.Text.RegularExpressions;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0192: scans text for the secret patterns that an agent might leak into
/// a committed file. Defense-in-depth for p0191's master-prompt rule
/// "apply credentials at user-config level only". Returns one match per
/// pattern-hit (file path + line number); empty list = clean.
/// Pattern set is deliberately narrow to keep false-positive noise low —
/// extend on need, not speculatively.
/// </summary>
public sealed class SecretPatternScanner : ISecretPatternScanner
{
    private static readonly Regex[] Patterns =
    [
        // NuGet.Config <add key="ClearTextPassword" value="…" />. We flag the
        // structural marker regardless of value placement — its mere presence
        // in a committed file means the agent inlined a credential.
        new(@"<add\s+key\s*=\s*[""']ClearTextPassword[""']",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // npm token line in .npmrc: //host/path/:_authToken=XYZ
        new(@"_authToken\s*=\s*(?<v>[A-Za-z0-9._\-+/=]{16,})",
            RegexOptions.Compiled),
        // Anthropic OAuth + workspace API keys.
        new(@"\bsk-ant-(?:oat|api)\w{8,}", RegexOptions.Compiled),
        // OpenAI keys.
        new(@"\bsk-[A-Za-z0-9_\-]{20,}", RegexOptions.Compiled),
        // GitHub PATs.
        new(@"\bghp_[A-Za-z0-9]{20,}", RegexOptions.Compiled),
        // GitLab PATs.
        new(@"\bglpat-[A-Za-z0-9_\-]{20,}", RegexOptions.Compiled),
        // Generic JWT (header.payload.signature, all base64url).
        new(@"\beyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\b",
            RegexOptions.Compiled),
    ];

    public IReadOnlyList<SecretMatch> Scan(string path, string content)
    {
        if (string.IsNullOrEmpty(content)) return Array.Empty<SecretMatch>();
        var matches = new List<SecretMatch>();
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            foreach (var pattern in Patterns)
            {
                if (pattern.IsMatch(lines[i]))
                    matches.Add(new SecretMatch(path, i + 1, pattern.ToString()));
            }
        }
        return matches;
    }
}
