namespace AgentSmith.Contracts.Services;

/// <summary>2026-11-24-9dc0d: how a provider counts a call's output against its per-minute token limit.</summary>
public enum OutputCount
{
    /// <summary>The output is not counted (Anthropic's input limit, local providers).</summary>
    None,

    /// <summary>The request's max_tokens is counted at admission and kept (OpenAI).</summary>
    MaxTokens,

    /// <summary>The tokens actually generated are counted once the call answered (Azure OpenAI).</summary>
    Generated,
}
