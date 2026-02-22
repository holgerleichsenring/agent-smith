using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Parses user input into a TicketId and ProjectName using regex patterns.
/// Supports formats like "fix #123 in todo-list", "#123 todo-list", "todo-list #123".
/// </summary>
public sealed class RegexIntentParser(
    ILogger<RegexIntentParser> logger) : IIntentParser
{
    private static readonly string[] NoiseWords =
        ["fix", "resolve", "close", "implement", "add", "update", "ticket", "issue", "in", "for"];

    private static readonly Regex TicketIdPattern = new(
        @"#?(\d+)", RegexOptions.Compiled);

    public Task<ParsedIntent> ParseAsync(
        string userInput, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userInput);

        var ticketId = ExtractTicketId(userInput);
        var projectName = ExtractProjectName(userInput);

        logger.LogInformation(
            "Parsed intent: Ticket={TicketId}, Project={Project}", ticketId, projectName);

        return Task.FromResult(new ParsedIntent(
            new TicketId(ticketId), new ProjectName(projectName)));
    }

    private static string ExtractTicketId(string input)
    {
        var match = TicketIdPattern.Match(input);
        if (!match.Success)
            throw new ConfigurationException(
                $"Could not extract ticket ID from input: '{input}'");

        return match.Groups[1].Value;
    }

    private static string ExtractProjectName(string input)
    {
        var cleaned = TicketIdPattern.Replace(input, "").Trim();

        var words = cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => !NoiseWords.Contains(w, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (words.Count == 0)
            throw new ConfigurationException(
                $"Could not extract project name from input: '{input}'");

        return words[0].ToLowerInvariant();
    }
}
