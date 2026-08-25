using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-08-25-a508: the ONE key an answer to this run's question is filed under.
/// <para>
/// Two producers resolved it independently: the ask gate preferred the progress reporter's
/// job id and fell back to the run id, while the mid-run master question always used the run
/// id. A run that has both therefore had two keys into one inbox, and an answer written under
/// one of them landed in a slot the other half never reads. Resolution lives here so the key
/// a checkpoint is WRITTEN under is the key a resume READS BACK.
/// </para>
/// </summary>
public interface IDialogueJobIdentity
{
    /// <summary>
    /// The dialogue job id for this run, or null when the run carries neither a job id nor a
    /// run id — the only case in which a question has nowhere to be answered.
    /// </summary>
    string? Resolve(PipelineContext pipeline);
}
