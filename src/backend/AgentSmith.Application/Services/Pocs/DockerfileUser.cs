using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: the final stage's last USER, when there is one, is numeric — a uid or uid:gid. The pod runs as
/// 10001 regardless (2026-11-23-c81eb); a named user cannot be checked against that, so it is refused.
/// </summary>
public sealed partial class DockerfileUser
{
    internal PocRuleRefusal? RefusalOf(DockerfileDocument document)
    {
        var user = document.FinalStage.LastOrDefault(i => i.Keyword == "USER");
        if (user is null || Numeric().IsMatch(user.Arguments)) return null;
        return new PocRuleRefusal(PocArchiveChecks.Dockerfile,
            $"Dockerfile line {user.Line}: USER {user.Arguments} is refused; name a numeric uid or uid:gid, because the "
            + "PoC runs under a fixed numeric user and a user name cannot be checked against it.");
    }

    [GeneratedRegex(@"^\d+(:\d+)?$")]
    private static partial Regex Numeric();
}
