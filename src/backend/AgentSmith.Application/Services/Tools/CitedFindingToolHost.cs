using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-30-03e1: the carrier for one finding that names the entry of the standard it
/// breaks.
/// <para>
/// This path is for a CITED finding and nothing else. A finding no entry covers — a logic
/// flaw inside an identity helper, a configured id that grants administrative rights, a
/// security-shaped setting no code reads — is refused here and belongs in the observation
/// array, where it reaches the reader unchanged. Three of the five findings this shape was
/// measured against are of that kind, so a rule that dropped them would suppress the very
/// class the inversion exists to recover.
/// </para>
/// <para>
/// It travels as its OWN tool call, never inside the final answer. That answer is a closed
/// contract — a single JSON array, with everything outside it discarded — and wrapping it
/// in an object is the degraded branch that once shipped raw untriaged scanner output.
/// </para>
/// </summary>
public sealed class CitedFindingToolHost(
    CitedFindingRecorder recorder, CitedFindingLog log, PipelineContext run) : IToolHost
{
    public const string ToolName = "record_cited_finding";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(RecordCitedFinding, name: ToolName)];
    }

    [Description(
        "Records ONE finding that breaks a named entry of the published verification "
        + "standard, at one station of one entry group. Look the entry up first with "
        + "look_up_requirements. Cite something you READ this run. A finding no entry of the "
        + "standard covers does NOT belong here — put it in your observation array, where it "
        + "is delivered exactly as any other finding is.")]
    public string RecordCitedFinding(
        [Description("The entry group this finding is about — the same name you gave "
            + "record_entry_station for it.")]
        string group,
        [Description("One of: admission, evidence, resolution, authority, scope, effect.")]
        string station,
        [Description("The requirement id exactly as look_up_requirements printed it. There "
            + "is no path through this tool without one.")]
        string requirement_id,
        [Description("What is wrong here, in one or two sentences.")]
        string detail,
        [Description("'member' — one member of the group, cited by file and line. 'group' — "
            + "the whole group at once, cited by the members in covers_members.")]
        string scope,
        [Description("For a member finding: the repo-relative file it rests on. It must be a "
            + "file you READ this run — a path you inferred resolves against nothing. Empty "
            + "for a group-wide finding.")]
        string file,
        [Description("For a member finding: the line it rests on. 0 otherwise.")]
        int start_line,
        [Description("For a group-wide finding: the members you generalise over, as "
            + "repo-relative paths separated by commas or newlines — every one a file you "
            + "read this run. Empty for a member finding.")]
        string covers_members = "") =>
        recorder.Record(run, log, new CitedFindingRequest(
            group, station, requirement_id, detail, scope, file, start_line, covers_members));
}
