using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0504: one context's resolved domain profile, paired with the working directory
/// that context occupies inside the sandbox. Two contexts sharing an image collapse
/// into ONE sandbox, so the verify path reads the full per-sandbox context list —
/// otherwise whether a domain is honoured would depend on discovery order.
/// </summary>
public sealed record DomainProfileStages(DomainProfile Profile, string Workdir);
