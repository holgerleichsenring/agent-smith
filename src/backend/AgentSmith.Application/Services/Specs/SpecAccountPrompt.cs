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
        IReadOnlyList<string> criteria, string diff, IReadOnlyList<string> commandResults,
        IReadOnlyList<string>? searchable = null)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(commandResults);
        // The FILE LIST is never truncated, the CONTENT is. Most criteria ask whether
        // something exists — an inventory document, a new extension class — and a
        // truncated diff answered "no" for a whole repository whose files were simply
        // past the budget. What changed is cheap to state; how it changed is not.
        var files = CitedFileIndex.FromDiff(diff);
        var changed = files.IsEmpty
            ? "(no file changed)"
            : string.Join("\n", files.Paths.OrderBy(p => p, StringComparer.Ordinal).Select(p => "- " + p));
        var body = diff ?? string.Empty;
        var list = string.Join("\n", criteria.Select(c => "- " + c));
        // p0483: an absence is settled by LOOKING, not by being shown a command that looked.
        var absence = searchable is { Count: > 0 }
            ? "A criterion about something being ABSENT you settle YOURSELF: call search_branch\n"
              + "against each repository the criterion covers and read what comes back. No\n"
              + "output means the branch does not contain it, and that is the proof. The\n"
              + $"repositories you can search are: {string.Join(", ", searchable)}. Do not close\n"
              + "an absence criterion on a listed command when you could look instead, and do\n"
              + "not report one as unsatisfied without having searched for it."
            : "A criterion about something being ABSENT is answered by the commands listed\n"
              + "under COMMANDS: no diff shows what a repository does NOT contain.";
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
            contains a build log. It is answered by the commands listed under COMMANDS, which
            really ran against this branch: cite the command, not a file.

            {{absence}}

            "citations" is a LIST and every element is ONE whole thing: one path from the
            file list, or one command copied VERBATIM from between the quotes on its line,
            with nothing added and nothing summarised. A command contains semicolons, pipes
            and ampersands of its own, so never join two commands into one element and never
            cut one apart — two commands are two elements. A long command is listed
            SHORTENED, with … standing for the part left out; copy it exactly as it is
            listed, marker included, and never restore what it hides. A description of what
            the commands did, however accurate, names no command and does not count.

            A listed command satisfies a criterion only when it COVERS it. A build or test
            criterion is covered by a command that exited 0. An ABSENCE criterion is covered
            by a search that ran and found nothing — such a search exits non-zero because it
            found nothing, and that IS the proof. The search's reach must be at least the
            criterion's: a criterion about the whole repository is not satisfied by a search
            of one directory or one file glob, and when the reach falls short say which part
            went unsearched. A search that could not run at all — a bad path, an unreadable
            tree, a tool that errored — proves nothing. Where a search and the diff
            disagree, the DIFF wins: the agent's commands ran at some point during the work,
            while the diff is what the branch carries now.

            For any other criterion, name the file that satisfies it. The FILE LIST below is
            complete; the DIFF BODY below may be only PART of what the branch changed, so a
            file's absence from it says nothing — check the list. A criterion about a file
            EXISTING is answered by the list alone.
            A criterion you cannot tie to a file in the diff is NOT satisfied, whatever it
            looks like it ought to be. Saying "not satisfied" costs you nothing and is the
            useful answer; saying "satisfied" without a file is the one thing that misleads.

            Answer with JSON and nothing else:

              [{"criterion": "<verbatim>", "satisfied": true|false,
                 "citations": ["<one diff path, or one command copied whole>", "..."],
                 "note": "<one short sentence>"}]

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
