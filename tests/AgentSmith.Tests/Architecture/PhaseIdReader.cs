using System.Text.RegularExpressions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0509: reads a phase id out of the three places the record states one — a spec's
/// <c>phase:</c> field, a context key, a <c>requires:</c> entry.
/// <para>
/// The framework mints its own ids: <c>PhaseIdFactory.For</c> turns ticket 19106 into
/// p19106a. A four-digit reading truncates that to p1910, so two phases of one ticket
/// collapse onto one id, the context key matches nothing at all, and a <c>requires:</c>
/// naming it resolves to a phase nobody wrote.
/// </para>
/// <para>
/// The widening is ADDITIVE and ANCHORED, never a replacement. <c>Counter</c> is the
/// reading installed with the rule and stays byte-identical, because some eighty legacy
/// specs put the whole slug in the <c>phase:</c> field
/// (<c>p0169j-a-frozen-trail-persistence</c>) and are read by PREFIX. <c>Whole</c> wins
/// only where it reaches the END of the value, which is exactly where the value IS an id
/// and nothing else: p19106a keeps its fifth digit and p0131c-pre keeps its tail, while
/// every slug-bearing legacy spec still falls through to <c>Counter</c> and reads as it
/// always did.
/// </para>
/// </summary>
internal sealed class PhaseIdReader
{
    private const string Counter = @"p\d{4}[a-z]?";
    private const string Whole = @"p\d{4,}[a-z]?(?:-[a-z][a-z0-9]*)?";

    // p0507: the second namespace. A date-minted id is the UTC date plus a four-hex
    // random suffix, and the suffix's FIXED WIDTH is what says where the id ends — so
    // it reads correctly by prefix, exactly like Counter, whether or not a slug follows.
    private const string Minted = @"\d{4}-\d{2}-\d{2}-[0-9a-f]{4}";

    /// <summary>The reading in force.</summary>
    public static PhaseIdReader Current { get; } = new(
        specId: $@"^\s*phase:\s*""?(?<id>{Whole}(?=""?\s*$)|{Minted}|{Counter})",
        // The colon is the anchor here, so one widened branch says what two would.
        contextId: $@"^    (?<id>{Whole}|{Minted}):",
        inlineRequires: $@"{Whole}(?=\s*(?:""|'|,|\]|$))|{Minted}|{Counter}",
        blockRequires: $@"^\s*-\s*(?<id>{Whole}(?=""?\s*$)|{Minted}|{Counter})");

    /// <summary>
    /// The four-digit reading exactly as it stood before p0509, kept so the widening can
    /// be PROVEN not to have moved the violation set out from under p0430's ratchet.
    /// </summary>
    public static PhaseIdReader Legacy { get; } = new(
        specId: $@"^\s*phase:\s*""?(?<id>{Counter})",
        contextId: $@"^    (?<id>{Counter}):",
        inlineRequires: Counter,
        blockRequires: $@"^\s*-\s*(?<id>{Counter})");

    private readonly Regex _inlineRequires;
    private readonly Regex _blockRequires;

    private PhaseIdReader(
        string specId, string contextId, string inlineRequires, string blockRequires)
    {
        SpecId = Compiled(specId);
        ContextId = Compiled(contextId);
        _inlineRequires = Compiled(inlineRequires);
        _blockRequires = Compiled(blockRequires);
    }

    public Regex SpecId { get; }

    public Regex ContextId { get; }

    public IEnumerable<string> Requires(string text)
    {
        // Both spellings the specs use: an inline list and a block list.
        var inline = Regex.Match(text, @"^requires:\s*\[(?<items>[^\]]*)\]", RegexOptions.Multiline);
        if (inline.Success)
            return _inlineRequires.Matches(inline.Groups["items"].Value).Select(m => m.Value);

        var block = Regex.Match(
            text, @"^requires:\s*\n(?<body>(?:[ \t]*-[ \t]*.*\n)+)", RegexOptions.Multiline);
        return block.Success
            ? _blockRequires.Matches(block.Groups["body"].Value).Select(m => m.Groups["id"].Value)
            : [];
    }

    private static Regex Compiled(string pattern) =>
        new(pattern, RegexOptions.Multiline | RegexOptions.Compiled);
}
