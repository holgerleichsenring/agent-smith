namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-ffa7: the ways a cut review may say a phase cannot be delivered, composed
/// from what the reviewer was given rather than numbered in the prompt's template.
/// <para>
/// Two predicates decide what is on offer. The coverage verdict needs the WHOLE ticket
/// (p0446); the false premise needs a look, because it is admitted only by an id the
/// framework minted for a look the reviewer took. Four combinations, one composition.
/// </para>
/// </summary>
public static class SpecCutVerdicts
{
    public const string Contradiction = "contradiction";
    public const string Uncheckable = "uncheckable";
    public const string NotInTicket = "not-in-ticket";

    /// <summary>The one verdict admitted by a citation instead of a quote.</summary>
    public const string FalsePremise = "false-premise";

    /// <summary>The problem keys on offer, in the order they are numbered.</summary>
    public static IReadOnlyList<string> Offered(bool wholeTicket, bool canLook)
    {
        var offered = new List<string> { Contradiction, Uncheckable };
        if (wholeTicket) offered.Add(NotInTicket);
        if (canLook) offered.Add(FalsePremise);
        return offered;
    }

    /// <summary>The numbered verdict list, and the withheld-coverage note when the ticket
    /// is not whole — its count read off the list, never written into it.</summary>
    public static string Describe(IReadOnlyList<string> offered, bool wholeTicket)
    {
        var lines = offered.Select((key, i) => $"{i + 1}. {Text(key)}").ToList();
        if (!wholeTicket)
            lines.Add(
                $"The ticket below is TOO LONG TO SHOW and has been cut off. Judge only the {Count(offered.Count)}\n"
                + "ways above. Never report that the ticket does not ask for something: the\n"
                + "part that asks for it may be in what you were not shown.");
        return string.Join("\n", lines);
    }

    /// <summary>How many ways, as the prompt says it.</summary>
    public static string Count(int count) => count switch
    {
        2 => "two",
        3 => "three",
        4 => "four",
        _ => count.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private static string Text(string key) => key switch
    {
        Contradiction =>
            "CONTRADICTION — two criteria of the SAME phase cannot both hold in one branch\n"
            + "   state. \"No production source file is modified\" and \"the old library appears\n"
            + "   nowhere in the sources\" is the shape: they belong to two phases.",
        Uncheckable =>
            "UNCHECKABLE — a criterion nobody can check against a repository or a command:\n"
            + "   a statement about the PROCESS (\"no push has been performed\", \"the work was\n"
            + "   done in the agreed order\") or about intent rather than result.",
        NotInTicket => "NOT IN THE TICKET — a criterion the ticket never asked for.",
        FalsePremise =>
            "FALSE PREMISE — the phase rests on something about the repositories that is not\n"
            + "   so — a file, a type, a package, a version or a place it assumes — and a look you\n"
            + "   took shows otherwise. Quote what the phase states that rests on it, and cite the\n"
            + "   evidence id of the look that shows it.",
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "not a cut-review verdict"),
    };
}
