namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-10-12-6bfac: a context's stack.image that the operator's registry boundary will not
/// let a sandbox run. <see cref="Reason"/> says what happens to it; <see cref="Lever"/> says
/// what the operator changes to use it, or to replace it.
/// </summary>
public sealed record ContextImageRefusal(string Image, string Reason, string Lever)
{
    /// <summary>One sentence for a tool result, a pull-request body or a run finding.</summary>
    public string Describe() => $"{Reason} — {Lever}";
}
