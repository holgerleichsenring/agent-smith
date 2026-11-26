using AgentSmith.Application.Services.SpecExecution;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0512: the spec record — 2026-11-26-462aa: contexts/default/done.md, one line per shipped spec — located once and
/// read by the same parser the record step writes it with.
/// <para>
/// p0499 shipped after four entries had been written broken, because the rule judging them GREPPED the YAML they
/// lived in: an inner <c>\"</c> ended the value as far as a regex was concerned. A rule that MEASURES an entry must
/// measure the string the writer wrote, so it reads through <see cref="ContextDoneIndex"/>.
/// </para>
/// </summary>
internal static class SpecRecordFile
{
    public static string Path { get; } = System.IO.Path.Combine(
        ArchitectureSources.AgentSmithRoot, "contexts", "default", "done.md");

    public static string ContextsRoot { get; } =
        System.IO.Path.Combine(ArchitectureSources.AgentSmithRoot, "contexts");

    public static string SpecsRoot { get; } =
        System.IO.Path.Combine(ArchitectureSources.AgentSmithRoot, "specs");

    public static string Text() => File.ReadAllText(Path);

    /// <summary>Every shipped spec's id paired with the entry recorded against it.</summary>
    public static IReadOnlyList<(string SpecId, string Entry)> DoneEntries() =>
        [.. ContextDoneIndex.Entries(Text()).Select(e => (e.Key, e.Value))];
}
