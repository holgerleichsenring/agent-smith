namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: data snapshot the master passes to each sub-agent. Three slots,
/// all string-typed and immutable: <see cref="PipelineGoal"/> tells the
/// child what the run is for; <see cref="PriorContextSlice"/> is the
/// observation block the master chose to share (decision anchors only,
/// not raw bus events — those flow via the lazy read_sub_agent_observations
/// tool); <see cref="OptionalSystemPromptBlock"/> is a free-form addendum
/// the master may inject if the task needs a specialised stance.
///
/// <para><b>Data only.</b> No cache_control fields. The cache architecture
/// (cache_control breakpoints, prefix warming, byte-identical fork) is
/// the topic of p0178; parallel fan-out does not naturally hit the cache
/// — that is a separate problem class.</para>
/// </summary>
public sealed record InheritedContext(
    string PipelineGoal,
    string PriorContextSlice,
    string? OptionalSystemPromptBlock = null);
