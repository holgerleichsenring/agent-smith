using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-09-22-5891: the surface a CHILD of a design turn runs under — the design surface
/// (<see cref="AgenticToolSurface.SpecDialog"/>) minus the two tools a concurrent child
/// cannot use.
/// <para>
/// <c>ask_human</c>, because a session holds exactly one pending question keyed by the
/// session: a second child's question replaces the first's and the answer is delivered only
/// to the question still standing, so two children asking would strand each other.
/// </para>
/// <para>
/// <c>remember</c>, because the read-only source scope refuses that write by THROWING rather
/// than answering it — a refusal the model cannot read is not a refusal, and a child that
/// insists dies on consecutive tool errors.
/// </para>
/// <para>
/// A pure function over the parent's list rather than a method on the surface: that class
/// stands at the 120-line limit, and the child list is DERIVED from what the parent was
/// granted — deriving it here keeps the two from drifting apart.
/// </para>
/// </summary>
public static class SpecDialogChildTools
{
    /// <summary>The design surface without the two tools a child cannot use.</summary>
    public static IList<AITool> Of(IList<AITool> specDialogSurface)
    {
        ArgumentNullException.ThrowIfNull(specDialogSurface);
        return [.. specDialogSurface.Where(tool =>
            !string.Equals(tool.Name, "ask_human", StringComparison.Ordinal)
            && !string.Equals(tool.Name, "remember", StringComparison.Ordinal))];
    }
}
