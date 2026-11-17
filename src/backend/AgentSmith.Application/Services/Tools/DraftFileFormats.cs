namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-12-cd4cg: the formats the design master writes its draft in, carried by the tool instead of a fetched
/// format. 2026-11-15-06fc: one per draft file, on write_file. 2026-11-17-5701a: a ticket carries its specs.
/// </summary>
internal static class DraftFileFormats
{
    public const string Spec =
        "A spec, specs/<id>.yaml, YAML in English whatever the conversation's language. goal (required): one terse sentence. "
        + "repos: the one repository it changes, as the prompt lists it (a parent names none). scope: {in, out} and no "
        + "other key. steps: [{id, action}] with one imperative line each. decisions: lines; a choice you make where "
        + "the code leaves it open starts with 'proposed:'. tests: Method_Scenario_Expected names. done: what a "
        + "reviewer answers yes or no against the code, one line each, or {given?, when, then}. facts: [{claim, "
        + "evidence}] for what you read, evidence a repository-relative path with lines. assumptions: what it rests on "
        + "that you did not read. requires: ids of specs in this draft (a child names siblings, never the parent), "
        + "or a sentence for an external precondition.";

    public const string Others =
        " An epic's parent, epic/<id>.yaml: the same format; it aggregates the specs under specs/ and changes no "
        + "repository itself. contract.yaml, the epic's series contract: names: [{name, kind, number?, owner, writers?}], "
        + "data: [{datum, lives_in, owner, writers?}], areas: [{spec, paths}], budgets: [{path, spec, max_lines}]; "
        + "owner, writers and spec name a child by its id; kind is one of event, system-event, table, context-key, "
        + "endpoint, config, command, tool, type; no '*' in a path. A ticket, tickets/<key>.yaml, YAML in English: "
        + "title (one line), requirement (its scope in prose, against the code as it stands once its requires are "
        + "merged), done (outcomes true once it has merged, one line each, a scenario as WHEN … THEN …), requires (keys "
        + "of tickets in this draft that merge first, or ticket:<tracker id> of a filed ticket), repos (the configured "
        + "repositories it changes); one run builds, reviews and merges it. A ticket's specs, "
        + "tickets/<key>/<label>.yaml, in the spec format above, each with a label no other spec or ticket of the "
        + "draft carries: the ticket is their parent, their requires name specs of the same ticket, their repos lie "
        + "within the ticket's when it names any, and the turn reviews them as it reviews a spec; the run the ticket "
        + "starts works off them. bug.yaml, a small fix filed as one fix-bug ticket: title (one imperative line), description "
        + "(what is wrong, where, and what correct behaviour looks like), acceptance_criteria (how the fix is "
        + "verified, one line each). comment.md: the comment for the bound ticket's readers, in the ticket's language.";
}
