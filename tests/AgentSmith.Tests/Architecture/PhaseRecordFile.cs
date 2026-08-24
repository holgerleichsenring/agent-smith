using YamlDotNet.RepresentationModel;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0512: the phase record — <c>state.done</c> in contexts/default/context.yaml —
/// located once and read by a YAML parser rather than a regex.
/// <para>
/// p0499 shipped after four entries had been written broken, because the rule judging
/// them GREPPED this file. Every entry is doubly quoted, so an inner <c>\"</c> ends the
/// value as far as a regex is concerned and everything after it is invisible. A rule
/// that MEASURES an entry would then measure the wrong string and pass a 3000-character
/// essay, which is worse than having no rule at all.
/// </para>
/// </summary>
internal static class PhaseRecordFile
{
    public static string Path { get; } = System.IO.Path.Combine(
        ArchitectureSources.AgentSmithRoot, "contexts", "default", "context.yaml");

    public static string PhasesRoot { get; } =
        System.IO.Path.Combine(ArchitectureSources.AgentSmithRoot, "phases");

    public static string Text() => File.ReadAllText(Path);

    /// <summary>Every shipped phase's id paired with the entry recorded against it.</summary>
    public static IReadOnlyList<(string PhaseId, string Entry)> DoneEntries()
    {
        var stream = new YamlStream();
        using var reader = new StringReader(Text());
        stream.Load(reader);

        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        var state = (YamlMappingNode)root.Children[new YamlScalarNode("state")];
        return state.Children[new YamlScalarNode("done")] is YamlMappingNode done
            ? [.. done.Children.Select(pair =>
                (((YamlScalarNode)pair.Key).Value ?? string.Empty,
                 ((YamlScalarNode)pair.Value).Value ?? string.Empty))]
            : [];
    }
}
