using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// IProgressReporter implementation for local CLI mode.
/// Writes progress to the console and reads answers from stdin.
/// Headless mode auto-approves all questions with the default answer.
/// </summary>
public sealed class ConsoleProgressReporter(
    ILogger<ConsoleProgressReporter> logger,
    bool headless = false) : IProgressReporter
{
    public Task ReportProgressAsync(int step, int total, string commandName,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[{Step}/{Total}] {Command}...", step, total, commandName);
        return Task.CompletedTask;
    }

    public Task<bool> AskYesNoAsync(string questionId, string text, bool defaultAnswer = true,
        CancellationToken cancellationToken = default)
    {
        if (headless)
        {
            logger.LogInformation("Headless mode: auto-answering '{QuestionId}' with {Default}",
                questionId, defaultAnswer ? "yes" : "no");
            return Task.FromResult(defaultAnswer);
        }

        Console.WriteLine();
        Console.WriteLine(text);
        Console.Write($"Answer (y/n) [{(defaultAnswer ? "y" : "n")}]: ");

        var input = Console.ReadLine()?.Trim().ToLowerInvariant();

        var answer = input switch
        {
            "y" or "yes" => true,
            "n" or "no" => false,
            null or "" => defaultAnswer,
            _ => defaultAnswer
        };

        return Task.FromResult(answer);
    }

    public Task ReportDoneAsync(string summary, string? prUrl = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Done: {Summary}", summary);

        if (!string.IsNullOrWhiteSpace(prUrl))
            logger.LogInformation("Pull Request: {PrUrl}", prUrl);

        return Task.CompletedTask;
    }

    public Task ReportErrorAsync(string text,
        int step = 0, int total = 0, string stepName = "",
        CancellationToken cancellationToken = default)
    {
        if (step > 0)
            logger.LogError("Error at [{Step}/{Total}] {StepName}: {Text}", step, total, stepName, text);
        else
            logger.LogError("Error: {Text}", text);

        return Task.CompletedTask;
    }

    public Task ReportDetailAsync(string text,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("  [detail] {Text}", text);
        return Task.CompletedTask;
    }
}
