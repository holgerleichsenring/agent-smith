namespace AgentSmith.Contracts.Services;

/// <summary>
/// Reports pipeline progress and asks interactive questions during execution.
/// Implementations: ConsoleProgressReporter (local/CLI), RedisProgressReporter (K8s job mode).
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Reports the current pipeline step progress.
    /// </summary>
    Task ReportProgressAsync(int step, int total, string commandName,
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

    /// <summary>
    /// Reports a fine-grained detail event during agentic execution.
    /// In Slack mode, posted as a thread reply under the progress message.
    /// In CLI mode, logged at Debug level (visible with --verbose).
    /// </summary>
    Task ReportDetailAsync(string text,
        CancellationToken cancellationToken);
}
