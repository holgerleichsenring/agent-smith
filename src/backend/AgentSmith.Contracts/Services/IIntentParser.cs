using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Parses free-form user input into a structured intent. Post-p0146b the canonical
/// implementation is LlmIntentParser — operators can write the request in any language
/// or phrasing and the LLM resolves it against the configured projects + pipeline catalog.
/// </summary>
public interface IIntentParser
{
    /// <summary>
    /// Legacy entry: extracts ticket id + project only. Used by the CLI dry-run flow
    /// and ExecutePipelineUseCase's string-based ExecuteAsync overload.
    /// </summary>
    Task<ParsedIntent> ParseAsync(string userInput, CancellationToken cancellationToken);

    /// <summary>
    /// p0146e: rich-result parse — resolves both pipeline and project (plus optional
    /// ticket). Consumed by the PR/MR comment handlers, where the post-slash body
    /// can be free-form text in any language.
    /// </summary>
    Task<PipelineRequest> ParseToPipelineRequestAsync(
        string userInput, string configPath, CancellationToken cancellationToken);
}
