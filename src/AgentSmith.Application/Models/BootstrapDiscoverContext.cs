using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0161d: context for the read-only BootstrapDiscover round. One Discover
/// round is emitted per RepoConnection during cold-init: the LLM lists the
/// repo's independently-deployable / independently-callable components with
/// evidence, the handler projects the result into
/// ContextKeys.DiscoveredComponents, and BootstrapDispatchHandler fans out
/// one BootstrapRound per component.
///
/// RepoName scopes the round to the repo's sandbox (Sandboxes[RepoName]) and
/// ProjectMap (RepoProjectMaps[RepoName]). Empty string in single-repo
/// back-compat runs (handler falls back to legacy keys when RepoName is empty).
///
/// The tool surface for this round is read-only filesystem +
/// (optionally) ask_human — no write_file, no run_command, no http_request.
/// </summary>
public sealed record BootstrapDiscoverContext(
    string RepoName,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
