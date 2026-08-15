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
    public static string For(
        IReadOnlyList<string> criteria, string diff, IReadOnlyList<string> commandResults)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(commandResults);
        // The FILE LIST is never truncated, the CONTENT is. Most criteria ask whether
        // something exists — an inventory document, a new extension class — and a
        // truncated diff answered "no" for a whole repository whose files were simply
        // past the budget. What changed is cheap to state; how it changed is not.
        var files = new DiffFileIndex(diff ?? string.Empty);
        var changed = files.IsEmpty
            ? "(no file changed)"
            : string.Join("\n", files.Paths.OrderBy(p => p, StringComparer.Ordinal).Select(p => "- " + p));
        var body = diff ?? string.Empty;
        var list = string.Join("\n", criteria.Select(c => "- " + c));
        var ran = commandResults.Count == 0
            ? "(no verification command ran for this phase)"
            : string.Join("\n", commandResults.Select(r => "- " + r));
        return $$"""
            A phase of automated work has finished. Below are the completion criteria that
            were ratified BEFORE the work started, and the diff the branch carries.

            Your job is to find what is MISSING. Go criterion by criterion and decide
            whether THIS DIFF satisfies it. You did not do this work and have no account of
            it other than the diff — do not assume anything happened that the diff does not
            show.

            A criterion about a BUILD OR TEST RESULT is not answerable from a diff — no diff
            contains a build log. Those are answered by the commands listed under COMMANDS,
            which really ran against this branch: cite the command, not a file. Treat a
            criterion as satisfied when a listed command covers it and exited 0.

            For any other criterion, name the file that satisfies it. The FILE LIST below is
            complete; the DIFF BODY below may be only PART of what the branch changed, so a
            file's absence from it says nothing — check the list. A criterion about a file
            EXISTING is answered by the list alone.
            A criterion you cannot tie to a file in the diff is NOT satisfied, whatever it
            looks like it ought to be. Saying "not satisfied" costs you nothing and is the
            useful answer; saying "satisfied" without a file is the one thing that misleads.

            Answer with JSON and nothing else:

              [{"criterion": "<verbatim>", "satisfied": true|false,
                 "citation": "<path in the diff>", "note": "<one short sentence>"}]

            CRITERIA
            {{list}}

            COMMANDS THAT RAN AGAINST THIS BRANCH
            {{ran}}

            EVERY FILE THIS BRANCH CHANGED (complete, never truncated)
            {{changed}}

            DIFF
            {{body}}
            """;
    }
}
