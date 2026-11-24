using AgentSmith.Server.Services.Pocs.Variables;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>2026-11-23-933db: what Start posts — new (with a name) or replace (with the PoC), the target, and whether
/// the person confirmed the different-app question. 2026-11-23-933dg: and the PoC's variables, written by a
/// poc-variables run before poc-deploy is queued.</summary>
internal sealed record PocImportRequest(
    string? Choice, string? Target, string? Name, long? PocId, bool Confirmed, IReadOnlyList<PocVariableInput>? Variables = null);
