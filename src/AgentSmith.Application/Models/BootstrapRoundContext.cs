using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the producer-loop runtime that drives the matched bootstrap skill
/// (project-bootstrap). The handler resolves the skill from AvailableRoles by
/// name, builds a tool-bearing chat call exposing the per-context bootstrap
/// WriteFile subset (path-write-guard restricted to .agentsmith/contexts/&lt;ctx&gt;/
/// context.yaml + coding-principles.md), and persists the skill's Markdown
/// summary into SkillOutputs for downstream WriteRunResult capture.
///
/// p0158g: RepoName scopes the round to one configured repo so the handler
/// writes into the correct per-repo sandbox (Sandboxes[RepoName]) and uses
/// the per-repo ProjectMap (RepoProjectMaps[RepoName]). Empty string in
/// single-repo back-compat runs (handler falls back to legacy Sandbox /
/// ProjectMap when RepoName is empty).
///
/// p0161d: ContextName + Workdir scope the round to one discovered component.
/// The bootstrap skill's WriteFile targets are built from
/// ProjectMetaPaths.MetaDirFor(ContextName) and PathWriteGuard accepts that
/// directory only — never the flat root path, never a foreign context's path.
/// Empty ContextName falls back to the legacy flat layout for pre-p0161d
/// test fixtures.
/// </summary>
public sealed record BootstrapRoundContext(
    string SkillName,
    string RepoName,
    AgentConfig AgentConfig,
    PipelineContext Pipeline,
    string ContextName = "",
    string Workdir = ".") : ICommandContext;
