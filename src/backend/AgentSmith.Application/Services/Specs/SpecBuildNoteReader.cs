using AgentSmith.Contracts.Specs;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-8762m: reads <c>outcome.hand_forward</c> out of an executed spec's yaml — the record in
/// <c>specs/done/</c> or the record artifact. No outcome, no key or unreadable yaml read as
/// <see cref="SpecBuildNoteRead.None"/>; the scalar <c>missing</c> as a master that left no note.
/// </summary>
public static class SpecBuildNoteReader
{
    public const string Key = "hand_forward";
    public const string Missing = "missing";
    public const string RenamedKey = "renamed";
    public const string MovedKey = "moved";
    public const string PersistedKey = "persisted";
    public const string DeviationsKey = "deviations";

    public static SpecBuildNoteRead Read(string? yaml, string specId)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return SpecBuildNoteRead.None;
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root
                || Child(root, SpecOutcomeBlock.Key) is not YamlMappingNode outcome)
                return SpecBuildNoteRead.None;
            return Child(outcome, Key) switch
            {
                YamlScalarNode { Value: Missing } => SpecBuildNoteRead.NoNote,
                YamlMappingNode note => new SpecBuildNoteRead(new SpecBuildNote(specId,
                    List(note, RenamedKey), List(note, MovedKey), List(note, PersistedKey), List(note, DeviationsKey)), false),
                _ => SpecBuildNoteRead.None,
            };
        }
        catch (YamlException)
        {
            return SpecBuildNoteRead.None;
        }
    }

    private static YamlNode? Child(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var node) ? node : null;

    private static IReadOnlyList<string> List(YamlMappingNode note, string key) =>
        Child(note, key) is YamlSequenceNode items
            ? [.. items.Children.OfType<YamlScalarNode>().Select(i => i.Value ?? string.Empty).Where(v => v.Length > 0)]
            : [];
}

/// <summary>
/// 2026-10-13-8762m: a record's build note — the note, or <see cref="Missing"/> when the master left none.
/// <see cref="None"/> is a record with no hand_forward key (a spec found already satisfied, or no record).
/// </summary>
public sealed record SpecBuildNoteRead(SpecBuildNote? Note, bool Missing)
{
    public static readonly SpecBuildNoteRead None = new(null, false);
    public static readonly SpecBuildNoteRead NoNote = new(null, true);
}
