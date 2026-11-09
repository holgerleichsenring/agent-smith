namespace AgentSmith.Contracts.Prompts;

/// <summary>2026-11-09-0620a: what a stored prompt block holds.</summary>
public enum PromptBlockKind
{
    /// <summary>The call's tool definitions: name, description and parameter schema of each.</summary>
    Tools,

    /// <summary>One message as the provider received it: text, tool calls, tool results, image identities.</summary>
    Message,
}
