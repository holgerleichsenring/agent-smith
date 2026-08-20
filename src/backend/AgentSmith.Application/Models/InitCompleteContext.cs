using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0490: context for InitCompleteCommand — the init pipeline's last step. Carries the
/// operator's auto-accept as it was ticked at launch, the run's repos so each opened
/// pull request resolves its own ISourceProvider, and the work branch a local
/// repository fast-forwards its default branch onto.
/// </summary>
public sealed record InitCompleteContext(
    bool AutoComplete,
    BranchName SourceBranch,
    IReadOnlyList<RepoConnection> Configs,
    PipelineContext Pipeline) : ICommandContext;
