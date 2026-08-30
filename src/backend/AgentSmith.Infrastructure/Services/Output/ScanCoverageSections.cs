using AgentSmith.Contracts.Commands;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// 2026-08-30-3c12: the sections that say what the SCAN covered, appended beneath the
/// findings that say what it found.
/// <para>
/// They are one unit because they are one story told in two halves — where each station of
/// each entry group lives, then what each of those stations can answer — and because each
/// of them is empty on a run that did not state it, so a report from a run without either
/// is byte for byte the report it was before they existed.
/// </para>
/// </summary>
public static class ScanCoverageSections
{
    public static string Markdown(PipelineContext pipeline) =>
        EntryStationSection.Markdown(pipeline) + RequirementSection.Markdown(pipeline);
}
