using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// 2026-09-17-042ee: the whitelisted argument summary as a PERSON is shown it while a design
/// turn runs. Three differences from the line the event stream carries, each for a reason the
/// event stream does not have.
/// <para>
/// A REPOSITORY NAMED IN AN ARGUMENT OF ITS OWN JOINS THE LINE. The derivation look's read and
/// search take (repository, path) and (repository, pattern); over a multi-repository scope
/// "read_file src/Api.cs" names no repository at all.
/// </para>
/// <para>
/// AN ADDRESS IS REDUCED. An absolute http(s) url becomes its host; any other value keeps only
/// what precedes its first query or fragment mark, because a relative or scheme-less address
/// ("/v2/orders?token=…", "localhost:8080/x?token=…") would otherwise print its query whole.
/// </para>
/// <para>
/// A SEARCH PATTERN IS SHOWN AS IT WAS SEARCHED — neither host-reduced nor cut at a '?'. Both
/// are regex, and a pattern the operator cannot read is not evidence of what the turn looked
/// for. See the phase's decisions for what that means for what a line may carry.
/// </para>
/// </summary>
internal static class OperatorToolSummary
{
    private const string PatternKey = "pattern";
    private const string RepositoryKey = "repository";
    private static readonly char[] QueryMarks = ['?', '#'];
    private static readonly string[] PathKeys = ["path", "paths", "file", "files", "dir", "directory"];

    public static string Of(AIFunctionArguments arguments, string key, string rendered) =>
        WithRepository(arguments, key, Addressed(key, rendered));

    private static string Addressed(string key, string rendered) =>
        key == PatternKey
            ? rendered
            : string.Join(", ", rendered.Split(", ").Select(part => Address(part.Trim())));

    private static string Address(string part)
    {
        if (Uri.TryCreate(part, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return uri.Host;
        var at = part.IndexOfAny(QueryMarks);
        return at < 0 ? part : part[..at];
    }

    /// <summary>A path joins the repository AS a path; anything else — a pattern — stays a
    /// value beside it, because "repo/Dispatch" would read as a file that does not exist.</summary>
    private static string WithRepository(AIFunctionArguments arguments, string key, string rendered) =>
        arguments.TryGetValue(RepositoryKey, out var repo) && repo is string name && name.Length > 0
            ? PathKeys.Contains(key, StringComparer.Ordinal) ? $"{name}/{rendered}" : $"{name} {rendered}"
            : rendered;
}
