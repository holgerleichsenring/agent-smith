namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// 2026-11-17-166ae: one worker call's prompt in its two parts — <see cref="System"/>, the same on every call of a
/// conversation, and <see cref="Input"/>, what changes. A binary that takes no system prompt reads <see cref="Text"/>.
/// </summary>
public sealed record WorkerPrompt(string System, string Input)
{
    /// <summary>Both parts as one text, system part first.</summary>
    public string Text => System + Input;

    /// <summary>Characters of both parts — the size a failure names.</summary>
    public int Length => System.Length + Input.Length;
}
