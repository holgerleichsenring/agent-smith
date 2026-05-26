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
    bool headless) : IProgressReporter
{
    public Task ReportProgressAsync(int step, int total, string commandName,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("[{Step}/{Total}] {Command}...", step, total, commandName);
        return Task.CompletedTask;
    }

    public Task<bool> AskYesNoAsync(string questionId, string text, bool defaultAnswer,
        CancellationToken cancellationToken)
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

    public Task ReportDoneAsync(string summary, string? prUrl,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Done: {Summary}", summary);

        if (!string.IsNullOrWhiteSpace(prUrl))
            logger.LogInformation("Pull Request: {PrUrl}", prUrl);

        return Task.CompletedTask;
    }

    public Task ReportErrorAsync(string text,
        int step, int total, string stepName,
        CancellationToken cancellationToken)
    {
        if (step > 0)
            logger.LogError("Error at [{Step}/{Total}] {StepName}: {Text}", step, total, stepName, text);
        else
            logger.LogError("Error: {Text}", text);

        return Task.CompletedTask;
    }

    public Task ReportDetailAsync(string text,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("  [detail] {Text}", text);
        return Task.CompletedTask;
    }
}
