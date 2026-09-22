namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: one sandbox as the reap judgement sees it — the same facts on
/// both backends, so the rails are written once instead of twice. A Docker container
/// and a Kubernetes pod carry the same four labels under different key spellings;
/// reading them is the backend's job, deciding on them is not.
/// </summary>
/// <param name="Id">Container id on Docker, pod name on Kubernetes.</param>
/// <param name="ConversationId">
/// The design conversation this sandbox belongs to, or empty. Empty is what every
/// sandbox carries until 2026-09-22-2d11b holds one, and it is what makes the third
/// rail unreachable in a deployment that holds nothing.
/// </param>
public sealed record SandboxReapCandidate(
    string Id,
    string JobId,
    string RunId,
    string ConversationId,
    TimeSpan Age);
