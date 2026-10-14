using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: a spec's seven registers as the texts a cut may quote — goal; scope as its in and out;
/// each decision's key; each step's id and action; the tests; the done entries; each fact's claim and
/// evidence. A quote stands in a register when it is a run of one entry's words, never across two entries.
/// A yaml that does not read as a mapping has empty registers, so no quote stands in it.
/// </summary>
public sealed class SpecRegisters
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _entries;

    private SpecRegisters(IReadOnlyDictionary<string, IReadOnlyList<string>> entries) => _entries = entries;

    public static SpecRegisters Read(string yaml)
    {
        IReadOnlyDictionary<string, object?> map;
        try
        {
            map = OutcomeYamlReader.ReadMap(yaml ?? string.Empty);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return new SpecRegisters(new Dictionary<string, IReadOnlyList<string>>());
        }
        return new SpecRegisters(new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [SpecRegisterNames.Goal] = [.. Texts(map.GetValueOrDefault("goal"))],
            [SpecRegisterNames.Scope] = [.. Field(map, "scope", "in"), .. Field(map, "scope", "out")],
            [SpecRegisterNames.Decisions] = [.. Items(map, "decisions").SelectMany(d => Of(d, "key"))],
            [SpecRegisterNames.Steps] = [.. Items(map, "steps").SelectMany(s => Of(s, "id").Concat(Of(s, "action")))],
            [SpecRegisterNames.Tests] = [.. Items(map, "tests").SelectMany(Texts)],
            [SpecRegisterNames.Done] = [.. DoneCriterion.Lines(map).Where(line => line.Length > 0)],
            [SpecRegisterNames.Facts] = [.. Items(map, "facts").SelectMany(f => Of(f, "claim").Concat(Of(f, "evidence")))],
        });
    }

    /// <summary>The register's texts, empty for a register the spec does not state or a name that is none.</summary>
    public IReadOnlyList<string> Entries(string register) =>
        _entries.TryGetValue(register ?? string.Empty, out var entries) ? entries : [];

    /// <summary>True when the quote is a run of the words of one of the register's texts.</summary>
    public bool Holds(string register, string? quote) =>
        Entries(register).Any(text => SpecQuotableText.IsRunIn(text, quote));

    private static IEnumerable<string> Field(IReadOnlyDictionary<string, object?> map, string key, string field) =>
        map.GetValueOrDefault(key) is Dictionary<object, object?> nested ? Of(nested, field) : Texts(map.GetValueOrDefault(key));

    private static IEnumerable<object?> Items(IReadOnlyDictionary<string, object?> map, string key) =>
        map.GetValueOrDefault(key) switch
        {
            List<object?> list => list,
            null => [],
            var single => [single],
        };

    // An item written as a plain line is its own text; a mapping contributes the field asked for.
    private static IEnumerable<string> Of(object? item, string field) => item switch
    {
        Dictionary<object, object?> entry => Texts(entry.GetValueOrDefault(field)),
        _ => field is "key" or "id" or "claim" ? Texts(item) : [],
    };

    private static IEnumerable<string> Texts(object? value) => value switch
    {
        string text when !string.IsNullOrWhiteSpace(text) => [text.Trim()],
        List<object?> lines => lines.SelectMany(Texts),
        _ => [],
    };
}
