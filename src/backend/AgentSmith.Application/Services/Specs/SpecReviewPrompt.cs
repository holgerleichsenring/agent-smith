namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// The question put to a fresh instance BEFORE any work exists: can each criterion of this
/// phase be satisfied at all, in THIS repository?
/// <para>
/// The delivery account asks whether a diff satisfies a criterion. This asks whether
/// anything could. The difference matters because a criterion is written from ticket prose
/// before anyone has looked at the code, so it can encode a guess about the SHAPE of the
/// fix — which files will change — that the repository is free to refuse. Read as prose the
/// guess is invisible; crossed with the repository it is obvious.
/// </para>
/// <para>
/// Asked with a deliberate bias toward passing. A wrong "outstanding" from the account
/// costs one repair pass; a wrong finding here costs a human's next working day, and three
/// of those teach them to stop reading. What cannot be demonstrated by running something
/// passes.
/// </para>
/// </summary>
public static class SpecReviewPrompt
{
    /// <summary>The prompt's opening sentence, declared so a caller can RECOGNISE this call
    /// among a run's other model calls without matching on prose that may be reworded.</summary>
    public const string Marker = "A phase of automated work is about to start.";

    public static string For(string goal, IReadOnlyList<string> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var list = string.Join("\n", criteria.Select(c => "- " + c));
        return $$"""
            {{Marker}} Below is its goal and the completion
            criteria that will BIND it: the run is judged done only when every one of them
            carries a "met" disposition. No work has happened yet, and the repositories are
            checked out for you to look at.

            Your job is to find the criteria that CANNOT BE MET, however good the work is.

            Go criterion by criterion and ask one mechanical question:

              Does this criterion describe the STATE OF THE WORLD after the work,
              or the SHAPE OF THE SOLUTION?

            A criterion about state names something an observation can settle: a command that
            exits 0, a report with no findings left, a file that exists, a value a config
            carries. An agent can make it true by working, and can see that it is true.

            A criterion about shape prescribes HOW the fix must look — which files must
            change, which mechanism must be used, what the diff must contain. The repository
            decides whether that is possible, and it may decide no. A criterion that names the
            files the fix must touch is the common case: whether those files change is a
            property of the problem, not of the effort.

            A criterion nothing can observe is the second defect: it rests on a judgement —
            "the ones worth fixing", "appropriate coverage", "clean code" — so no command
            makes it true or false and no disposition can honestly be written for it.

            The third is a criterion that ALREADY HOLDS, before any work. Report it; do not
            treat it as harmless.

            DISPOSITIONS — one per criterion:
              decidable              an observation settles it and the agent can make it true
              prescribes_shape       it constrains the solution's shape; the repository may refuse it
              no_observation_settles nothing observable makes it true or false
              already_true           it holds right now, before any work

            EVIDENCE IS THE POINT. A finding with nothing behind it is an opinion, and an
            opinion sent to the person who wrote this ticket is worse than silence. You have a
            read-only search over the checked-out repositories. For every row that is NOT
            decidable you must SEARCH for the thing the criterion talks about and report what
            came back:
              "observation" the search you ran, verbatim
              "output"      what it returned, trimmed to the part that carries the point

            A search that finds nothing is evidence: a criterion naming a file, a package or a
            symbol the repository does not carry is a criterion about a world this repository
            is not in. A search you did not run is not evidence, and neither is a description
            of one.

            If you cannot demonstrate a defect by looking, the criterion is decidable. Say so and move on. Do not reason your way to a finding, do not report
            style, wording or ambition, and never report a criterion merely because it is
            demanding — a hard criterion that an observation settles is a good criterion.

            For every non-decidable row, also write "replacement": the criterion this one
            should become, stated so an observation settles it. Keep the same ambition — a
            replacement that asks for less has changed what done means, which is not yours to
            do. Where the world offers no way to satisfy it at all, the replacement is the
            criterion plus a documented exception.

            Answer with a JSON array, one object per criterion, in the order given:
            [{"criterion": "...", "disposition": "...", "observation": "...", "output": "...",
              "replacement": "...", "note": "..."}]

            No prose outside the array.

            GOAL
            {{goal}}

            CRITERIA
            {{list}}
            """;
    }
}
