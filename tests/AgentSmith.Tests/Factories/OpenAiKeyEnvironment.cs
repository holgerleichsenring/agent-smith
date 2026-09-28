namespace AgentSmith.Tests.Factories;

/// <summary>Tests that set OPENAI_API_KEY run one at a time: it is process-wide.</summary>
[CollectionDefinition(nameof(OpenAiKeyEnvironment), DisableParallelization = true)]
public sealed class OpenAiKeyEnvironment;
