namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-08-25-1360: what the account is TOLD, separated from what it is SHOWN.
/// <para>
/// The prompt assembles evidence — criteria, a file list, a body, a command list. These are
/// the standing rules for reading that evidence, and they change for different reasons: a
/// rule moves when the account is reasoning wrongly, the assembly moves when the account is
/// being handed the wrong thing. Keeping them in one file is also what pushed it past the
/// length the architecture rule allows, which is the rule noticing the same seam.
/// </para>
/// </summary>
internal static class AccountEvidenceRules
{
    /// <summary>
    /// p0483: an absence is settled by LOOKING, not by being shown a command that looked.
    /// Without a sandbox there is nothing to look with, and the account is told so rather
    /// than being invited to search something that cannot answer.
    /// </summary>
    public static string Absence(IReadOnlyList<string>? searchable) =>
        searchable is { Count: > 0 }
            ? "A criterion about something being ABSENT you settle YOURSELF: call search_branch\n"
              + "against each repository the criterion covers and read what comes back. No\n"
              + "output means the branch does not contain it, and that is the proof. The\n"
              + $"repositories you can search are: {string.Join(", ", searchable)}. Do not close\n"
              + "an absence criterion on a listed command when you could look instead, and do\n"
              + "not report one as unsatisfied without having searched for it. A criterion you\n"
              + "settled this way is CITED BY THE PATTERN you searched for, copied exactly as\n"
              + "you wrote it — that search is evidence like any command, and a criterion you\n"
              + "searched and then cite nothing for is refused."
            : "A criterion about something being ABSENT is answered by the commands listed\n"
              + "under COMMANDS: no diff shows what a repository does NOT contain.";

    /// <summary>
    /// 2026-08-25-0eae: the base is reachable, and the account is told so. A tool nobody is
    /// told about ships inert — the same way p0483's search would have, had its rule not been
    /// written at the same time. 2026-08-25-9749 adds what the base is FOR, now that the
    /// disposition exists to express the answer.
    /// </summary>
    public static string Base(IReadOnlyList<string>? baseSearchable) =>
        baseSearchable is { Count: > 0 }
            ? "You can also read the BASE — the code as it stood BEFORE this delivery — with\n"
              + "search_base. That is the only way to answer a question about what was there to\n"
              + $"begin with. The repositories with a base you can search are: {string.Join(", ", baseSearchable)}.\n"
              + "A search of the base is cited by its pattern like any other, and it is a\n"
              + "different fact from a search of the branch: one says what is there now, the\n"
              + "other what was there before."
            : string.Empty;

    /// <summary>
    /// 2026-08-25-9749: the third disposition, offered ONLY where a base can be searched.
    /// It is admitted by a base search that ran and found nothing plus a named antecedent,
    /// so where there is no base to search it could never be admitted — and a disposition
    /// that always degrades to "not satisfied" is a false-refusal generator wearing the
    /// costume of a fix.
    /// </summary>
    public static string NotApplicable(IReadOnlyList<string>? baseSearchable) =>
        baseSearchable is { Count: > 0 }
            ? "A criterion can also be CONDITIONAL — it applies only WHERE something was\n"
              + "already the case (\"every host that already configures X …\", \"wherever Y was\n"
              + "previously set up\"). If the base never contained that thing anywhere, the\n"
              + "criterion has nothing to apply to. That is neither satisfied nor unsatisfied:\n"
              + "answer \"not_applicable\", put the precondition you found absent in\n"
              + "\"antecedent\", and cite the search_base pattern that found nothing. Without\n"
              + "BOTH — a base search that ran and matched nothing, and a named antecedent —\n"
              + "\"not_applicable\" is read as NOT SATISFIED. It is not a way to set a criterion\n"
              + "aside: where the base DOES contain the precondition, answer normally, and a\n"
              + "criterion that states a prohibition or a plain requirement is never conditional."
            : string.Empty;

    /// <summary>The answer shape, which carries the third disposition only where it can be
    /// admitted. Naming a disposition the account cannot use is how a rule teaches an answer
    /// the checker will refuse.</summary>
    public static string AnswerShape(IReadOnlyList<string>? baseSearchable) =>
        baseSearchable is { Count: > 0 }
            ? "  [{\"criterion\": \"<verbatim>\", \"disposition\": \"satisfied\" | \"not_applicable\" | \"not_satisfied\",\n"
              + "     \"citations\": [\"<one diff path, or one command copied whole>\", \"...\"],\n"
              + "     \"antecedent\": \"<not_applicable only: the precondition the base does not contain>\",\n"
              + "     \"note\": \"<one short sentence>\"}]"
            : "  [{\"criterion\": \"<verbatim>\", \"disposition\": \"satisfied\" | \"not_satisfied\",\n"
              + "     \"citations\": [\"<one diff path, or one command copied whole>\", \"...\"],\n"
              + "     \"note\": \"<one short sentence>\"}]";
}
