using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0315d: builds the coding master's user prompt for a phase-execution run —
/// the validated phase spec (verbatim, untrusted-delimited) plus the
/// spec-first execution contract (steps in order, decisions logged, tests
/// run, every done criterion verified) and the operator's answers to prior
/// open questions when the run was re-triggered after a clarification park.
/// p0317: the ticket's conversation + attachments sections (pre-rendered by
/// the master handler, delimited as untrusted content) follow the spec — on a
/// re-triggered run the comment thread carries the operator's answers.
/// </summary>
public interface IPhaseExecutionPromptFactory
{
    string Build(
        PipelineContext pipeline, Ticket ticket, Repository repository, IEnumerable<string> sandboxKeys,
        string conversationSection, string attachmentsSection);
}
