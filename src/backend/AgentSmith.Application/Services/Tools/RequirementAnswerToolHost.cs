using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-30-3c12: the carrier for one answer to one entry of the standard.
/// <para>
/// The answers travel as their OWN tool calls, never inside the final answer. That answer
/// is a closed contract — a single JSON array, with everything outside it discarded — and
/// wrapping it in an object is the degraded branch that once shipped raw untriaged scanner
/// output. One entry per call, so a worker states a station a piece at a time and corrects
/// one row without re-emitting the rest.
/// </para>
/// </summary>
public sealed class RequirementAnswerToolHost(
    RequirementAnswerRecorder recorder, RequirementAnswerLog log, PipelineContext run) : IToolHost
{
    public const string ToolName = "record_requirement_answer";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(RecordRequirementAnswer, name: ToolName)];
    }

    [Description(
        "Answers ONE entry of the published verification standard at ONE station of ONE "
        + "entry group, for reads or for writes. Call list_station_requirements first: only "
        + "the entries it lists are asked. Every verdict cites something you READ this run, "
        + "or names the input you lacked.")]
    public string RecordRequirementAnswer(
        [Description("The entry group this answer is about — the same name you gave "
            + "record_entry_station for it.")]
        string group,
        [Description("One of: admission, evidence, resolution, authority, scope, effect.")]
        string station,
        [Description("The requirement id exactly as list_station_requirements printed it.")]
        string requirement_id,
        [Description("'read' for operations that return state, 'write' for operations that "
            + "change it. A resource scoped on read and unscoped on write is answered twice, "
            + "once each way — that asymmetry is what this separation exists to surface.")]
        string operation,
        [Description("'met' — satisfied here; 'unmet' — not satisfied here; 'cannot_answer' "
            + "— you could not decide, and missing_input says what you lacked.")]
        string verdict,
        [Description("'member' — this is about one member of the group, cited by file and "
            + "line. 'group' — this is about the whole group at once, cited by the members "
            + "in covers_members. A group-wide claim citing no member counts as unanswered.")]
        string scope,
        [Description("For a member answer: the repo-relative file the verdict rests on. It "
            + "must be a file you READ this run — a path you inferred resolves against "
            + "nothing. Empty for a group-wide answer.")]
        string file,
        [Description("For a member answer: the line the verdict rests on. 0 otherwise.")]
        int start_line,
        [Description("For a group-wide answer: the members you generalise over, as "
            + "repo-relative paths separated by commas or newlines — every one a file you "
            + "read this run. Empty for a member answer.")]
        string covers_members = "",
        [Description("For cannot_answer: the input you would have needed to decide, in one "
            + "sentence. Empty otherwise.")]
        string missing_input = "") =>
        recorder.Record(run, log, new RequirementAnswerRequest(
            group, station, requirement_id, operation, verdict, scope,
            file, start_line, covers_members, missing_input));
}
