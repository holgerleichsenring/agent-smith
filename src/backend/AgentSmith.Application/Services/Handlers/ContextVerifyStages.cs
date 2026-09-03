using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-08-31-26d4: one context's DECLARED verify stages.
/// <para>
/// Two contexts resolving the same image collapse into ONE sandbox, so the verify path
/// reads the full per-sandbox context list rather than the sandbox's representative
/// discovery — reading the representative would make whose stages run depend on
/// discovery order.
/// </para>
/// <para>
/// 2026-09-03-7bac: the pair no longer carries a working directory. Every command runs
/// at the repository root, and a command that needs another directory says so itself.
/// </para>
/// </summary>
/// <param name="DerivedFrom">2026-09-01-e14d: the files these stages were derived from and
/// their hash at that moment, so the run can re-hash them where they live — from the
/// repository root — and report a declaration whose source has moved. Null = the
/// declaration names no source, and the run has nothing to compare.</param>
public sealed record ContextVerifyStages(
    string ContextName, IReadOnlyList<ContextYamlVerifyStage> Stages,
    ContextYamlVerifyDerivation? DerivedFrom = null);
