using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-08-31-26d4: one context's DECLARED verify stages, paired with the working
/// directory that context occupies inside the sandbox.
/// <para>
/// Two contexts resolving the same image collapse into ONE sandbox, so the verify path
/// reads the full per-sandbox context list and carries each declaration's own workdir.
/// Reading the sandbox's representative discovery instead would make whose stages run
/// depend on discovery order.
/// </para>
/// </summary>
public sealed record ContextVerifyStages(
    string ContextName, IReadOnlyList<ContextYamlVerifyStage> Stages, string Workdir);
