using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-11-22-66e4a: the model quota this server process holds — per deployment, free tokens and requests of the
/// minute, refill and pause. <paramref name="Replica"/> names the process: each replica counts its own calls, so
/// behind a load balancer the answer is one replica's view, and the provider's remaining quota is the shared one.
/// </summary>
public sealed record ModelQuotaView(string Replica, IReadOnlyList<LlmQuotaSnapshot> Deployments);
