using System.Diagnostics;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Converts a document to Markdown via MarkItDown, detects contract type, loads legal skills.
/// </summary>
public sealed class BootstrapDocumentHandler(
    ILlmClientFactory llmClientFactory,
    ISkillLoader skillLoader,
    IPromptCatalog prompts,
    ILogger<BootstrapDocumentHandler> logger) : ICommandHandler<BootstrapDocumentContext>
{
    private const int ContractTypeDetectionMaxChars = 2000;

    public async Task<CommandResult> ExecuteAsync(
        BootstrapDocumentContext context, CancellationToken cancellationToken)
    {
        var workspace = context.Repository.LocalPath;
        var sourceFiles = Directory.GetFiles(workspace)
            .Where(f => !f.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (sourceFiles.Count == 0)
            return CommandResult.Fail("No source document found in workspace");

        var sourceFile = sourceFiles[0];
        var fileName = Path.GetFileNameWithoutExtension(sourceFile);

        var markdown = await ConvertToMarkdownAsync(sourceFile, cancellationToken);
        if (markdown is null)
            return CommandResult.Fail($"MarkItDown conversion failed for {Path.GetFileName(sourceFile)}");

        var markdownPath = Path.Combine(workspace, $"{fileName}.md");
        await File.WriteAllTextAsync(markdownPath, markdown, cancellationToken);
        context.Pipeline.Set(ContextKeys.DocumentMarkdown, markdown);
        logger.LogInformation("Converted {File} to Markdown ({Chars} chars)", Path.GetFileName(sourceFile), markdown.Length);

        var contractType = await DetectContractTypeAsync(markdown, context.Agent, cancellationToken);
        context.Pipeline.Set(ContextKeys.ContractType, contractType);
        logger.LogInformation("Detected contract type: {ContractType}", contractType);

        var roles = skillLoader.LoadRoleDefinitions(context.SkillsPath);
        context.Pipeline.Set(ContextKeys.AvailableRoles, roles);
        logger.LogInformation("Loaded {Count} legal skill roles", roles.Count);

        var principlesPath = Path.Combine(context.SkillsPath, "legal-principles.md");
        if (File.Exists(Path.Combine(context.Repository.LocalPath, principlesPath)))
            context.Pipeline.Set(ContextKeys.DomainRules, await File.ReadAllTextAsync(
                Path.Combine(context.Repository.LocalPath, principlesPath), cancellationToken));

        return CommandResult.Ok($"Bootstrapped {Path.GetFileName(sourceFile)} as {contractType}");
    }

    private async Task<string?> ConvertToMarkdownAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "markitdown",
                Arguments = $"\"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                logger.LogError("MarkItDown failed (exit {Code}): {Error}", process.ExitCode, error);
                return null;
            }

            return output;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run MarkItDown for {File}", filePath);
            return null;
        }
    }

    private async Task<string> DetectContractTypeAsync(
        string markdown, AgentConfig agentConfig, CancellationToken cancellationToken)
    {
        var snippet = markdown.Length > ContractTypeDetectionMaxChars
            ? markdown[..ContractTypeDetectionMaxChars]
            : markdown;

        var llmClient = llmClientFactory.Create(agentConfig);

        try
        {
            var llmResponse = await llmClient.CompleteAsync(
                prompts.Get("contract-classifier-system"), snippet, TaskType.Scout, cancellationToken);
            var contractType = llmResponse.Text.Trim().ToLowerInvariant();

            string[] validTypes = ["nda", "werkvertrag", "dienstleistungsvertrag", "saas-agb", "kaufvertrag", "mietvertrag"];
            return validTypes.Contains(contractType) ? contractType : "unknown";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Contract type detection failed, defaulting to 'unknown'");
            return "unknown";
        }
    }
}
