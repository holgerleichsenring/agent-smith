namespace AgentSmith.Server.Services.Init;

/// <summary>
/// p0489: the body of POST /api/projects/{name}/init — what the operator chose on the init
/// they are starting. 2026-10-04-2bf2: <c>RefreshPrinciples</c> is the operator's word to
/// recompose an existing principles.md on THIS init; a request that omits it preserves the file.
/// 2026-11-17-713ea: auto-accept is no longer a launch choice — every init completes its pull
/// requests, whichever door started it; a body that still sends the old field is ignored.
/// </summary>
public sealed record InitLaunchRequest(bool RefreshPrinciples = false);
