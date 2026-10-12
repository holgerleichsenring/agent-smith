namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-10-12-50bda: what the sandbox image chain decided, and which link decided it.
/// <para>
/// <see cref="RefusedContextImage"/> is the context's stack.image the registry boundary
/// refused, carried BESIDE the image the sandbox is started in instead of being dropped: a
/// refused declaration is not something the chain may quietly replace, and the code run's
/// preflight stops on it. An image is still named because every sandbox needs one to check
/// out into, and pipelines that verify nothing keep working in it.
/// </para>
/// </summary>
public sealed record SandboxImageDecision(
    string Image, SandboxImageLink Link, string? RefusedContextImage = null)
{
    /// <summary>True when the context declared an image the sandbox does not run.</summary>
    public bool SubstitutesTheDeclaredImage =>
        RefusedContextImage is not null
        && !string.Equals(RefusedContextImage, Image, StringComparison.Ordinal);

    /// <summary>True when the generic image was left because nothing named or declared one.</summary>
    public bool FellThrough => Link == SandboxImageLink.GenericFallback && RefusedContextImage is null;
}
