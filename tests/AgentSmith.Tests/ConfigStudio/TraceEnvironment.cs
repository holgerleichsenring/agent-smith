namespace AgentSmith.Tests.ConfigStudio;

/// <summary>Tests that set AGENTSMITH_TRACE run one at a time: it is process-wide.</summary>
[CollectionDefinition(nameof(TraceEnvironment), DisableParallelization = true)]
public sealed class TraceEnvironment;
