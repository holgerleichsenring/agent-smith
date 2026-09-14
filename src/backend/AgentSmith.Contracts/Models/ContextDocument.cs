namespace AgentSmith.Contracts.Models;

/// <summary>
/// One meta file read from a sandbox — a context.yaml or a principles.md — with the context
/// it belongs to. 2026-09-04-cf3d: a sandbox holds every context that shares its toolchain
/// image, so what a loader hands the master is a LIST of these, each attributed to the
/// context whose subtree it governs. <see cref="ContextName"/> and <see cref="Workdir"/> are
/// null for the flat pre-contexts file at the repository root, which speaks for the whole
/// sandbox.
/// </summary>
public sealed record ContextDocument(
    string SandboxKey, string? ContextName, string? Workdir, string Path, string Content);
