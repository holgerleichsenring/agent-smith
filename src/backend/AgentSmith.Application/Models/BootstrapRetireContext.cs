using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-09-23-4711: context for the BootstrapRetire step. The handler reads
/// <c>ContextKeys.DiscoveredComponents</c> — the set this run derived — and the context
/// directories each repository's sandbox actually carries, and moves aside every name on
/// the tree side only.
/// </summary>
public sealed record BootstrapRetireContext(PipelineContext Pipeline) : ICommandContext;
