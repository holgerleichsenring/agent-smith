namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: the question put to a fresh instance — the criteria, the diff, and the
/// instruction to find what is MISSING.
/// <para>
/// Asked negatively on purpose: "all done" is the cheap answer to the positive question
/// and the expensive answer to this one. And the reader is told it has no account of the
/// work other than the diff, because a model that believes it did the work confirms
/// itself.
/// </para>
/// </summary>
public static class SpecAccountPrompt
{
    private const int MaxDiffChars = 60_000;

    public static string For(IReadOnlyList<string> criteria, string diff)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var body = Fit(diff ?? string.Empty);
        var list = string.Join("\n", criteria.Select(c => "- " + c));
        return $$"""
            A phase of automated work has finished. Below are the completion criteria that
            were ratified BEFORE the work started, and the diff the branch carries.

            Your job is to find what is MISSING. Go criterion by criterion and decide
            whether THIS DIFF satisfies it. You did not do this work and have no account of
            it other than the diff — do not assume anything happened that the diff does not
            show.

            For a criterion you call satisfied, name the file in the diff that satisfies it.
            A criterion you cannot tie to a file in the diff is NOT satisfied, whatever it
            looks like it ought to be. Saying "not satisfied" costs you nothing and is the
            useful answer; saying "satisfied" without a file is the one thing that misleads.

            Answer with JSON and nothing else:

              [{"criterion": "<verbatim>", "satisfied": true|false,
                 "citation": "<path in the diff>", "note": "<one short sentence>"}]

            CRITERIA
            {{list}}

            DIFF
            {{body}}
            """;
    }

    private static string Fit(string diff) =>
        diff.Length <= MaxDiffChars
            ? diff
            : diff[..MaxDiffChars] + "\n… diff truncated; judge only what is shown and say so.";
}
