using System.Text.RegularExpressions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-11-16-3596: answers whether a C# source calls a tracker or repository WRITE member — the
/// rule "everything is a pipeline" judges the server project with it.
/// <para>
/// The members are read from the write side of ITicketProvider, ISourceProvider and its partials,
/// IPrCommentProvider, ITicketStatusTransitioner, ITicketRequiresLink and IPullRequestCloser. A
/// write member added to one of those interfaces is added here, or the ratchet does not see it.
/// CreateAsync and CloseAsync are common names, so they count only in a file that references the
/// interface they belong to.
/// </para>
/// </summary>
internal static class ServerWritesScanner
{
    private static readonly Regex DistinctWrite = new(
        @"\.(AddLabelAsync|CloseTicketAsync|TransitionToAsync|TransitionAsync|UpdateStatusAsync|FinalizeAsync"
        + @"|PostCommentAsync|PostReviewBatchAsync|DeleteCommentsByMarkerAsync|RewriteRegionAsync"
        + @"|WriteFilesToBranchAsync|CreatePullRequestAsync|CompletePullRequestAsync|MarkPullRequestReadyAsync"
        + @"|RetargetPullRequestAsync|UpdatePullRequestBodyAsync|LinkAsync)\(",
        RegexOptions.Compiled);

    private static readonly Regex Create = new(@"\.CreateAsync\(", RegexOptions.Compiled);
    private static readonly Regex Close = new(@"\.CloseAsync\(", RegexOptions.Compiled);

    public static bool Writes(string source) =>
        DistinctWrite.IsMatch(source)
        || (source.Contains("ITicketProvider", StringComparison.Ordinal) && Create.IsMatch(source))
        || (source.Contains("IPullRequestCloser", StringComparison.Ordinal) && Close.IsMatch(source));
}
