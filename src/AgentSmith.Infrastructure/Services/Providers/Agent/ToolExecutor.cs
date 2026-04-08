using System.Text.Json.Nodes;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Executes tool calls from the AI agent against the local repository.
/// Delegates command execution to CommandRunner, handles file operations directly.
/// </summary>
public sealed class ToolExecutor(
    string repositoryPath,
    ILogger logger,
    FileReadTracker? fileReadTracker = null,
    IProgressReporter? progressReporter = null,
    IDialogueTransport? dialogueTransport = null,
    IDialogueTrail? dialogueTrail = null,
    string? jobId = null)
{
    private const int MaxFileSizeBytes = 100 * 1024;
    private static readonly TimeSpan DefaultQuestionTimeout = TimeSpan.FromMinutes(5);

    private readonly CommandRunner _commandRunner = new(repositoryPath, logger, progressReporter);
    private readonly List<CodeChange> _changes = new();
    private readonly List<PlanDecision> _decisions = new();

    public IReadOnlyList<CodeChange> GetChanges() => _changes.AsReadOnly();
    public IReadOnlyList<PlanDecision> GetDecisions() => _decisions.AsReadOnly();

    public async Task<string> ExecuteAsync(string toolName, JsonNode? input)
    {
        try
        {
            return toolName switch
            {
                "read_file" => ReadFile(input),
                "write_file" => WriteFile(input),
                "list_files" => ListFiles(input),
                "run_command" => await _commandRunner.RunAsync(input),
                "log_decision" => LogDecision(input),
                "ask_human" => await AskHumanAsync(input),
                _ => $"Error: Unknown tool '{toolName}'."
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tool execution failed: {Tool}", toolName);
            return $"Error: {ex.Message}";
        }
    }

    private string ReadFile(JsonNode? input)
    {
        var path = GetStringParam(input, "path");
        ValidatePath(path);

        var fullPath = Path.Combine(repositoryPath, path);
        if (!File.Exists(fullPath))
            return $"Error: File not found: {path}";

        if (fileReadTracker is not null && fileReadTracker.HasBeenRead(path))
        {
            fileReadTracker.TrackRead(path);
            logger.LogDebug("File {Path} already read, returning short reference", path);
            return $"[File previously read: {path}. Content unchanged since last read.]";
        }

        var info = new FileInfo(fullPath);
        string content;
        if (info.Length > MaxFileSizeBytes)
        {
            logger.LogWarning("File {Path} exceeds size limit, truncating", path);
            content = File.ReadAllText(fullPath);
            content = content[..Math.Min(content.Length, MaxFileSizeBytes)]
                      + "\n... [truncated]";
        }
        else
        {
            content = File.ReadAllText(fullPath);
        }

        fileReadTracker?.TrackRead(path);
        ReportDetail($"\ud83d\udcc4 Reading: {path}");
        return content;
    }

    private string WriteFile(JsonNode? input)
    {
        var path = GetStringParam(input, "path");
        var content = GetStringParam(input, "content");
        ValidatePath(path);

        var fullPath = Path.Combine(repositoryPath, path);
        var changeType = File.Exists(fullPath) ? "Modify" : "Create";

        var directory = Path.GetDirectoryName(fullPath);
        if (directory is not null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(fullPath, content);
        _changes.Add(new CodeChange(new FilePath(path), content, changeType));
        fileReadTracker?.InvalidateRead(path);

        logger.LogDebug("Wrote file {Path} ({ChangeType})", path, changeType);
        ReportDetail($"\u270f\ufe0f Writing: {path}");
        return $"File written: {path}";
    }

    private string ListFiles(JsonNode? input)
    {
        var path = input?["path"]?.GetValue<string>() ?? "";
        if (!string.IsNullOrEmpty(path))
            ValidatePath(path);

        var fullPath = string.IsNullOrEmpty(path)
            ? repositoryPath
            : Path.Combine(repositoryPath, path);

        if (!Directory.Exists(fullPath))
            return $"Error: Directory not found: {path}";

        var files = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(repositoryPath, f))
            .Where(f => !f.StartsWith(".git" + Path.DirectorySeparatorChar))
            .OrderBy(f => f)
            .ToList();

        return string.Join('\n', files);
    }

    private string LogDecision(JsonNode? input)
    {
        var category = GetStringParam(input, "category");
        var decision = GetStringParam(input, "decision");

        _decisions.Add(new PlanDecision(category, decision));
        logger.LogDebug("Decision logged [{Category}]: {Decision}", category, decision);
        return $"Decision logged: [{category}] {decision}";
    }

    private async Task<string> AskHumanAsync(JsonNode? input)
    {
        if (dialogueTransport is null || jobId is null)
            return "Error: Dialogue transport not configured. Cannot ask human.";

        var questionTypeStr = GetStringParam(input, "question_type");
        var text = GetStringParam(input, "text");
        var context = GetStringParam(input, "context");
        var defaultAnswer = GetStringParam(input, "default_answer");
        var choicesNode = input?["choices"];

        var normalizedType = questionTypeStr.Replace("_", "");
        if (!Enum.TryParse<QuestionType>(normalizedType, ignoreCase: true, out var questionType))
            return $"Error: Invalid question_type '{questionTypeStr}'.";

        List<string>? choices = null;
        if (choicesNode is JsonArray arr)
            choices = arr.Select(c => c?.GetValue<string>() ?? "").Where(c => c.Length > 0).ToList();

        var questionId = Guid.NewGuid().ToString("N");
        var question = new DialogQuestion(
            questionId, questionType, text, context, choices?.AsReadOnly(),
            defaultAnswer, DefaultQuestionTimeout);

        ReportDetail($"\u2753 Asking human: {text}");
        await dialogueTransport.PublishQuestionAsync(jobId, question, CancellationToken.None);

        var answer = await dialogueTransport.WaitForAnswerAsync(
            jobId, questionId, DefaultQuestionTimeout, CancellationToken.None);

        string answerText;
        if (answer is null)
        {
            answerText = defaultAnswer;
            answer = new DialogAnswer(questionId, defaultAnswer, "timeout", DateTimeOffset.UtcNow, "system");
            logger.LogWarning("Question '{QuestionId}' timed out, using default answer: {Default}", questionId, defaultAnswer);
        }
        else
        {
            answerText = answer.Answer;
            logger.LogInformation("Received answer for '{QuestionId}': {Answer}", questionId, answerText);
        }

        if (dialogueTrail is not null)
            await dialogueTrail.RecordAsync(question, answer);

        var timedOut = answer.Comment == "timeout";
        return timedOut
            ? $"Answer (timeout, used default): {answerText}"
            : $"Answer: {answerText}";
    }

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty.");
        if (Path.IsPathRooted(path))
            throw new ArgumentException("Absolute paths are not allowed.");
        if (path.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Path traversal (..) is not allowed.");
    }

    private static string GetStringParam(JsonNode? input, string name)
    {
        var value = input?[name]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required parameter: {name}");
        return value;
    }

    private void ReportDetail(string text)
    {
        try { progressReporter?.ReportDetailAsync(text, CancellationToken.None).GetAwaiter().GetResult(); }
        catch (Exception ex) { logger.LogDebug(ex, "Detail reporting failed"); }
    }
}
