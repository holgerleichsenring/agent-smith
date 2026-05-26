using System.Diagnostics;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Executes shell commands within a repository with timeout and blocked-command safety.
/// </summary>
public sealed class CommandRunner(
    string repositoryPath,
    ILogger logger,
    IProgressReporter? progressReporter = null)
{
    private const int CommandTimeoutSeconds = 60;

    private static readonly string[] BlockedCommandPatterns =
    [
        "dotnet run", "dotnet watch",
        "npm start", "npm run dev", "npm run serve",
        "yarn start", "yarn dev",
        "node server",
        "python -m http.server", "python manage.py runserver",
        "flask run", "uvicorn ", "gunicorn ",
        "java -jar",
        "docker run", "docker compose up", "docker-compose up",
        "kubectl port-forward",
        "ng serve", "vite", "webpack serve",
    ];

    public async Task<string> RunAsync(JsonNode? input)
    {
        var command = input?["command"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Missing required parameter: command");

        if (IsBlockedCommand(command))
        {
            logger.LogWarning("Blocked command rejected: {Command}", command);
            return $"Error: Command rejected. Long-running server processes are not allowed " +
                   $"(matched blocked pattern). Use 'dotnet build' and 'dotnet test' to verify changes. " +
                   $"Command: {command}";
        }

        logger.LogInformation("Executing command: {Command}", command);
        ReportDetail($"\u25b6\ufe0f Running: {Truncate(command, 80)}");

        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(CommandTimeoutSeconds));

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
            WorkingDirectory = repositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            KillProcess(process);
            return $"Error: Command timed out after {CommandTimeoutSeconds} seconds.\nCommand: {command}";
        }

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        var result = string.IsNullOrEmpty(stderr)
            ? stdout
            : $"{stdout}\n[stderr]\n{stderr}";

        return $"Exit code: {process.ExitCode}\n{result}".Trim();
    }

    internal static bool IsBlockedCommand(string command)
    {
        var normalized = command.Trim();
        var segments = normalized.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var parts = segment.Split([";", "&&", "||"], StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                foreach (var pattern in BlockedCommandPatterns)
                {
                    if (trimmed.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }

        return false;
    }

    private void KillProcess(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to kill timed-out process"); }
    }

    private void ReportDetail(string text)
    {
        try { progressReporter?.ReportDetailAsync(text, CancellationToken.None).GetAwaiter().GetResult(); }
        catch (Exception ex) { logger.LogDebug(ex, "Detail reporting failed"); }
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length > maxLength ? text[..maxLength] + "..." : text;
}
