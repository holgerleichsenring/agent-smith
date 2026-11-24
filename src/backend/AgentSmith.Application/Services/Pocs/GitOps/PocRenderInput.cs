using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: everything one render reads — the PoC's name and namespace, its host and the port of the revision
/// the desired state names, the image by digest, the desired state, the target, the seal and the plain variables.
/// </summary>
public sealed record PocRenderInput(
    string Name, string Namespace, string Host, int Port, string Image, PocDesiredState Desired, PocTargetConfig Target,
    PocSealResult Seal, IReadOnlyList<PocPlainVariable> Plain);
