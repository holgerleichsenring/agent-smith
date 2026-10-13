namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-ffa7: the ways a cut review may say a spec cannot be delivered, composed
/// from what the reviewer was given rather than numbered in the prompt's template.
/// <para>
/// Two predicates decide what is on offer. The coverage verdict needs the WHOLE ticket
/// (p0446); the false premise needs a look, because it is admitted only by an id the
/// framework minted for a look the reviewer took. Four combinations, one composition.
/// </para>
/// <para>
/// 2026-09-15-a5a5: the ticket has three states, and the verdicts name what a spec STATES —
/// a criterion, or its goal and steps where it lists none — rather than assuming a criterion.
/// </para>
/// <para>
/// 2026-10-13-a347b: a dropped 'Done when' outcome needs the whole ticket too, and is the one
/// verdict whose quote comes from the ticket.
/// </para>
/// <para>
/// A shape rule is offered always and last: a done entry that measures the code's form is
/// judged from a diff by a model, which is not where a size or a layout is measured.
/// </para>
/// </summary>
public static class SpecCutVerdicts
{
    public const string Contradiction = "contradiction";
    public const string Uncheckable = "uncheckable";
    public const string NotInTicket = "not-in-ticket";

    /// <summary>2026-10-13-a347b: the one verdict that quotes the ticket rather than a spec.</summary>
    public const string DoneDropped = "done-dropped";

    /// <summary>The one verdict admitted by a citation instead of a quote.</summary>
    public const string FalsePremise = "false-premise";

    /// <summary>The one verdict admitted only against a done entry.</summary>
    public const string ShapeRule = "shape-rule";

    /// <summary>The problem keys on offer, in the order they are numbered.</summary>
    public static IReadOnlyList<string> Offered(CutReviewTicket ticket, bool canLook)
    {
        var offered = new List<string> { Contradiction, Uncheckable };
        if (ticket == CutReviewTicket.Whole) offered.AddRange([NotInTicket, DoneDropped]);
        if (canLook) offered.Add(FalsePremise);
        offered.Add(ShapeRule);
        return offered;
    }

    /// <summary>The numbered verdict list, and the withheld-coverage note when the ticket
    /// is not whole — its count read off the list, never written into it.</summary>
    public static string Describe(IReadOnlyList<string> offered, CutReviewTicket ticket)
    {
        var lines = offered.Select((key, i) => $"{i + 1}. {Text(key)}").ToList();
        if (ticket == CutReviewTicket.Truncated)
            lines.Add(
                $"The ticket below is TOO LONG TO SHOW and has been cut off. Judge only the {Count(offered.Count)}\n"
                + "ways above. Never report that the ticket does not ask for something: the\n"
                + "part that asks for it may be in what you were not shown.");
        if (ticket == CutReviewTicket.Absent)
            lines.Add(
                $"There is NO TICKET behind these specs. Judge only the {Count(offered.Count)} ways above.\n"
                + "Never report that something was not asked for: nothing was shown to ask for it.");
        return string.Join("\n", lines);
    }

    /// <summary>How many ways, as the prompt says it.</summary>
    public static string Count(int count) => count switch
    {
        2 => "two",
        3 => "three",
        4 => "four",
        5 => "five",
        6 => "six",
        _ => count.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private static string Text(string key) => key switch
    {
        Contradiction =>
            "CONTRADICTION — two things the SAME spec states cannot both hold in one branch\n"
            + "   state. \"No production source file is modified\" and \"the old library appears\n"
            + "   nowhere in the sources\" is the shape: they belong to two specs.",
        Uncheckable =>
            "UNCHECKABLE — something a spec states that nobody can check against a\n"
            + "   repository or a command: a statement about the PROCESS (\"no push has been performed\", \"the work was\n"
            + "   done in the agreed order\") or about intent rather than result.",
        NotInTicket => "NOT IN THE TICKET — something a spec states that the ticket never asked for.",
        DoneDropped =>
            "DONE DROPPED — an outcome the ticket lists under its own \"Done when\",\n"
            + "   \"Definition of done\" or \"Acceptance\" that no done entry of any spec states (a\n"
            + "   shape rule excepted). This way quotes the TICKET: quote the ticket's outcome\n"
            + "   verbatim, and give as spec_id the spec that does that work.",
        FalsePremise =>
            "FALSE PREMISE — the spec rests on something about the repositories that is not\n"
            + "   so — a file, a type, a package, a version or a place it assumes — and a look you\n"
            + "   took shows otherwise. Quote what the spec states that rests on it, and cite the\n"
            + "   evidence id of the look that shows it.",
        ShapeRule =>
            "SHAPE RULE — a done entry that measures the code's form rather than an outcome,\n"
            + "   however narrowly scoped: the size of functions, classes or files, line length,\n"
            + "   operators per line, code layout (indentation, wrapping, braces) or naming\n"
            + "   conventions. A visual design value or a named change (\"rename X to Y\") is not\n"
            + "   one. Quote the done entry; nothing else a spec states is reported this way.",
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "not a cut-review verdict"),
    };
}
