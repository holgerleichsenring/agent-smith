using System.Text;
using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Compiles run history from .agentsmith/runs/ into a wiki knowledge base
/// stored in .agentsmith/wiki/. Tracks last compiled run to avoid reprocessing.
/// </summary>
public sealed class CompileKnowledgeHandler(
    ILlmClient llmClient,
    ILogger<CompileKnowledgeHandler> logger)
    : ICommandHandler<CompileKnowledgeContext>
{
    private const string AgentSmithDir = ".agentsmith";
    private const string WikiDir = "wiki";
    private const string RunsDir = "runs";
    private const string LastCompiledFile = ".last-compiled";
    private const string IndexFile = "index.md";

    public async Task<CommandResult> ExecuteAsync(
        CompileKnowledgeContext context, CancellationToken cancellationToken)
    {
        var agentDir = Path.Combine(context.Repository.LocalPath, AgentSmithDir);
        var wikiDir = Path.Combine(agentDir, WikiDir);
        var runsDir = Path.Combine(agentDir, RunsDir);

        if (!Directory.Exists(runsDir))
        {
            logger.LogInformation("No runs directory found at {RunsDir}", runsDir);
            return CommandResult.Ok("No runs directory found, nothing to compile");
        }

        var runDirs = GetRunDirectories(runsDir);
        if (runDirs.Count == 0)
        {
            logger.LogInformation("No run directories found");
            return CommandResult.Ok("No runs found, nothing to compile");
        }

        var lastCompiled = context.FullRecompile ? 0 : ReadLastCompiled(wikiDir);
        var newRuns = runDirs
            .Where(r => r.RunNumber > lastCompiled)
            .OrderBy(r => r.RunNumber)
            .ToList();

        if (newRuns.Count == 0)
        {
            logger.LogInformation("Wiki is up to date (last compiled: r{LastCompiled:D2})", lastCompiled);
            return CommandResult.Ok("Wiki up to date");
        }

        Directory.CreateDirectory(wikiDir);

        var existingWiki = ReadExistingWiki(wikiDir);
        var runData = await ReadRunDataAsync(newRuns, cancellationToken);

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(existingWiki, runData);

        logger.LogInformation(
            "Compiling {Count} new run(s) into knowledge base (r{From:D2}..r{To:D2})",
            newRuns.Count, newRuns[0].RunNumber, newRuns[^1].RunNumber);

        var response = await llmClient.CompleteAsync(
            systemPrompt, userPrompt, TaskType.Summarization, cancellationToken);

        var wikiUpdates = ParseWikiUpdates(response.Text);
        if (wikiUpdates.Count == 0)
        {
            logger.LogWarning("LLM returned no wiki updates");
            return CommandResult.Fail("Failed to parse wiki updates from LLM response");
        }

        foreach (var (fileName, content) in wikiUpdates)
        {
            var filePath = Path.Combine(wikiDir, fileName);
            await File.WriteAllTextAsync(filePath, content, cancellationToken);
            logger.LogDebug("Updated wiki file: {File}", fileName);
        }

        var latestRun = newRuns[^1].RunNumber;
        await WriteLastCompiledAsync(wikiDir, latestRun, cancellationToken);

        context.Pipeline.Set(ContextKeys.WikiUpdates, wikiUpdates);

        var summary = $"Wiki updated with {newRuns.Count} run(s), {wikiUpdates.Count} file(s) written (up to r{latestRun:D2})";
        logger.LogInformation("{Summary}", summary);
        return CommandResult.Ok(summary);
    }

    internal static List<RunDirectoryInfo> GetRunDirectories(string runsDir)
    {
        var result = new List<RunDirectoryInfo>();
        foreach (var dir in Directory.GetDirectories(runsDir))
        {
            var name = Path.GetFileName(dir);
            if (name.Length >= 3 && name[0] == 'r' && int.TryParse(name[1..3], out var num))
            {
                result.Add(new RunDirectoryInfo(dir, num, name));
            }
        }

        return result.OrderBy(r => r.RunNumber).ToList();
    }

    internal static int ReadLastCompiled(string wikiDir)
    {
        var path = Path.Combine(wikiDir, LastCompiledFile);
        if (!File.Exists(path))
            return 0;

        var content = File.ReadAllText(path).Trim();
        return int.TryParse(content, out var num) ? num : 0;
    }

    private static string ReadExistingWiki(string wikiDir)
    {
        var indexPath = Path.Combine(wikiDir, IndexFile);
        if (!File.Exists(indexPath))
            return string.Empty;

        return File.ReadAllText(indexPath);
    }

    private static async Task<List<RunData>> ReadRunDataAsync(
        List<RunDirectoryInfo> runs, CancellationToken ct)
    {
        var result = new List<RunData>();
        foreach (var run in runs)
        {
            var planPath = Path.Combine(run.Path, "plan.md");
            var resultPath = Path.Combine(run.Path, "result.md");

            var plan = File.Exists(planPath)
                ? await File.ReadAllTextAsync(planPath, ct)
                : string.Empty;

            var runResult = File.Exists(resultPath)
                ? await File.ReadAllTextAsync(resultPath, ct)
                : string.Empty;

            result.Add(new RunData(run.RunNumber, run.Name, plan, runResult));
        }

        return result;
    }

    private static string BuildSystemPrompt() =>
        """
        You are a technical writer compiling a project knowledge base from AI agent run history.
        Your output must be a JSON object with a single key "wiki_updates" containing filename-content pairs.
        Each file should be valid Markdown. Create or update these files as needed:
        - index.md: Table of contents linking to all wiki pages
        - decisions.md: Architectural and design decisions made across runs
        - known-issues.md: Known bugs, limitations, and workarounds discovered
        - patterns.md: Coding patterns, conventions, and best practices established
        - Additional concept articles as warranted by the content

        Rules:
        - Synthesize information, don't just copy run data
        - Group related decisions together
        - Note when a later run supersedes an earlier decision
        - Use clear headings and bullet points
        - All text must be in English
        - Output ONLY valid JSON, no markdown fences or other text
        """;

    internal static string BuildUserPrompt(string existingWiki, List<RunData> runs)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(existingWiki))
        {
            sb.AppendLine("## Existing Wiki (index.md)");
            sb.AppendLine(existingWiki);
            sb.AppendLine();
        }

        sb.AppendLine("## New Run Data");
        sb.AppendLine();

        foreach (var run in runs)
        {
            sb.AppendLine($"### Run r{run.RunNumber:D2} ({run.DirName})");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(run.Plan))
            {
                sb.AppendLine("#### Plan");
                sb.AppendLine(run.Plan);
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(run.Result))
            {
                sb.AppendLine("#### Result");
                sb.AppendLine(run.Result);
                sb.AppendLine();
            }
        }

        sb.AppendLine("Please compile the above into wiki pages. Output JSON: { \"wiki_updates\": { \"filename.md\": \"content\" } }");
        return sb.ToString();
    }

    internal static Dictionary<string, string> ParseWikiUpdates(string llmResponse)
    {
        try
        {
            // Try to extract JSON from the response (may be wrapped in markdown fences)
            var json = llmResponse.Trim();
            if (json.StartsWith("```"))
            {
                var firstNewline = json.IndexOf('\n');
                var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewline > 0 && lastFence > firstNewline)
                    json = json[(firstNewline + 1)..lastFence].Trim();
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("wiki_updates", out var updates))
                return new Dictionary<string, string>();

            var result = new Dictionary<string, string>();
            foreach (var prop in updates.EnumerateObject())
            {
                var content = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(content))
                    result[prop.Name] = content;
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static async Task WriteLastCompiledAsync(
        string wikiDir, int runNumber, CancellationToken ct)
    {
        var path = Path.Combine(wikiDir, LastCompiledFile);
        await File.WriteAllTextAsync(path, runNumber.ToString(), ct);
    }

    internal sealed record RunDirectoryInfo(string Path, int RunNumber, string Name);
    internal sealed record RunData(int RunNumber, string DirName, string Plan, string Result);
}
