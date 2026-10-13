using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: one spec of the derived set — the schema-valid spec plus the markdown
/// companion that carries the ticket spans this spec is responsible for. Two
/// files, because a spec states WHAT and the manual's code templates are
/// what a summary destroys.
/// </summary>
/// <param name="MockPaths">
/// 2026-10-01-283dh: the HTML design mocks beside the spec's files in the spec directory —
/// repository-relative paths of every .html whose file name starts with the SPEC ID and then
/// '.' or '-'. Matched by id, never by stem, because a goal edit re-slugs the stem. Null when
/// the spec has none or was not read back off a branch. Appended last: the record is positional.
/// </param>
public sealed record DerivedSpec(
    SpecDraft Draft,
    string Slug,
    string Markdown,
    IReadOnlyList<int> CarriedSegments,
    IReadOnlyList<string>? MockPaths = null)
{
    /// <summary>Shared stem of the yaml and its markdown companion.</summary>
    public string FileStem => $"{Draft.SpecId}-{Slug}";

    public string SpecId => Draft.SpecId;
}
