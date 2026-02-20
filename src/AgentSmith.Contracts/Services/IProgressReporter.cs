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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the user a yes/no question and waits for an answer.
    /// Returns true for yes, false for no.
    /// In headless mode, implementations should return the default value.
    /// </summary>
    Task<bool> AskYesNoAsync(string questionId, string text, bool defaultAnswer = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports successful pipeline completion with an optional PR URL.
    /// </summary>
    Task ReportDoneAsync(string summary, string? prUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports a pipeline error with optional step context.
    /// </summary>
    Task ReportErrorAsync(string text,
        int step = 0, int total = 0, string stepName = "",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports a fine-grained detail event during agentic execution.
    /// In Slack mode, posted as a thread reply under the progress message.
    /// In CLI mode, logged at Debug level (visible with --verbose).
    /// </summary>
    Task ReportDetailAsync(string text,
        CancellationToken cancellationToken = default);
}
