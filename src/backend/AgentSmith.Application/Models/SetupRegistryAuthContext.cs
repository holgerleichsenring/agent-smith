using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0198: pre-stage private-feed credentials in each sandbox before any
/// downstream build/test step can hit NU1301 / EAUTH / 401. Handler pulls
/// the registries: block from injected AgentSmithConfig and the per-repo
/// sandboxes from the pipeline context.
/// </summary>
public sealed record SetupRegistryAuthContext(PipelineContext Pipeline) : ICommandContext;
