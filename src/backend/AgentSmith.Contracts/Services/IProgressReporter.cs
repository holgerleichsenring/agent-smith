using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Reports pipeline progress and asks interactive questions during execution.
/// Implementations: ConsoleProgressReporter (local/CLI), RedisProgressReporter (K8s job mode).
///
/// <para>p0173e: the previous <c>string commandName</c> parameter on
/// <see cref="ReportProgressAsync"/> is replaced by a typed
/// <see cref="PipelineCommand"/> reference, and the free-form
/// <c>ReportDetailAsync(string)</c> channel is removed — detail rows now
/// flow as typed <c>L1StepDetailEvent</c> on the event bus.</para>
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Reports the current pipeline step progress. The typed command carries
    /// the pipeline command + skill + round + repo + workdir so the reporter
    /// does not have to re-format a display string.
    /// </summary>
    Task ReportProgressAsync(int step, int total, PipelineCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asks the user a yes/no question and waits for an answer.
    /// Returns true for yes, false for no.
    /// In headless mode, implementations should return the default value.
    /// </summary>
    Task<bool> AskYesNoAsync(string questionId, string text, bool defaultAnswer,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reports successful pipeline completion.
    /// </summary>
    Task ReportDoneAsync(string summary, string? prUrl,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reports a pipeline error with step context.
    /// </summary>
    Task ReportErrorAsync(string text,
        int step, int total, string stepName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Optional job identifier used for dialogue transport routing.
    /// Returns null when not running in job mode (e.g., CLI).
    /// </summary>
    string? JobId => null;
}
