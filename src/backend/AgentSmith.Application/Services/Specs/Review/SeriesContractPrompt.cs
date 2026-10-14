using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762k: what the one fresh series-contract reviewer is asked — whether the members of a set keep the
/// contract they agreed on. The brief is in code: no skills release mid-series. Members are rendered whole while
/// the prompt stays under <see cref="MaxMembersChars"/>; past that each is shown in part — its goal, scope and
/// steps, and every fact and done line naming a path, name or datum of the contract — and the prompt says which.
/// </summary>
public static class SeriesContractPrompt
{
    public const int MaxMembersChars = 60_000;

    public const string Brief =
        "Check this series against its contract before any more of it is built. For every member ask: does it "
        + "mint a name, number or table the contract gives another member or does not list; persist a datum "
        + "anywhere but its declared home; touch an area another member claims; or contradict a line budget? "
        + "Answer with exactly one verdict — BUILD, BUILD WITH CUTS or REFUSE. A cut changes the member that "
        + "breaks the contract; REFUSE is a successful result when the members cannot all keep it.";

    public static string For(SeriesContractInput input, DerivationLook? look)
    {
        ArgumentNullException.ThrowIfNull(input);
        var head = $$"""
            {{Brief}}

            You did not write these specs and have no stake in them.

            ## How to answer

            Answer with ONE JSON object and nothing else:

              {"verdict": "BUILD" | "BUILD WITH CUTS" | "REFUSE",
               "cuts": [{"spec_id": "<the member you change>",
                         "register": "<one of {{string.Join(", ", SpecRegisterNames.All)}}>",
                         "quote": "<the exact words of that register of that member>",
                         "replacement": "<the new words, or null to remove them>",
                         {{Evidence(input.InRun)}}}],
               "findings": [{"text": "<what breaks the contract, one or two sentences>", {{Evidence(input.InRun)}}}]}

            - Every quote is a run of at least {{SpecAttackPrompt.QuoteWords}} consecutive words of the register it
              names in the member it names, copied exactly; a cut naming no member, or quoting anything else
              (requires, applies_to, the id), is discarded.
            {{Rule(input.InRun)}}
            {{SeriesContractPromptSection.Render(input.Contract)}}{{input.QuestionPin}}{{DerivationLookPromptSection.Render(look)}}
            ## The members

            """;
        return head + SeriesContractMembers.Render(input, MaxMembersChars - head.Length);
    }

    private static string Evidence(bool inRun) => inRun
        ? "\"repository\": \"<repository>\", \"path\": \"<path>\", \"line\": <line from your own read>, \"cites\": \"<the evidence id of that read>\""
        : "\"entry\": \"<the contract entry it breaks, copied verbatim from the list below>\"";

    private static string Rule(bool inRun) => inRun
        ? """
          - A cut and a finding rest on a file you READ with the tool, at a line that read returned — the member's
            spec file or the series manifest on the branch. Anything else is discarded.
          - A member marked executed is built and is never changed; a member marked not cleared is not built in
            this attempt. Cut only a member marked cleared; name a clash with the others as a finding.
          """
        : "- A cut and a finding rest on the contract entry they break, quoted verbatim. Anything else is discarded.";
}
