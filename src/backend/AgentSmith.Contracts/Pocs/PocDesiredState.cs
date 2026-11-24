namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-c81eb: what a PoC's manifests declare — how many pods, which revision with which image digest, and the
/// readiness path (null probes the port over TCP). The Poc row holds it and every render reads it; an act (c81ed) puts
/// the state it wants under ContextKeys.PocDesiredState, and only the GitOps commit writes it back to the row, after its
/// commit landed.
/// </summary>
public sealed record PocDesiredState(int Replicas, int Revision, string ImageDigest, string? ReadinessPath);
