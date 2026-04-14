using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

internal sealed class TriageResponseParser(ILogger logger)
{
    internal const string SystemPrompt =
        "You are triaging work to determine which specialist roles " +
        "should participate. Respond with valid JSON only, no markdown.";

    public static string BuildPrompt(string userPrompt, string rolesDescription) => $$"""
        {{userPrompt}}

        ## Available Roles
        {{rolesDescription}}

        ## Instructions
        Analyze the input and determine:
        1. Which roles are needed (select from available roles only)
        2. Who should lead the discussion (creates the initial analysis)
        3. For EVERY available role, explain why it is included or excluded

        Respond in JSON:
        {
          "lead": "role-name",
          "participants": ["role-name-1", "role-name-2"],
          "reasoning": {
            "role-name-1": "why this role is needed",
            "role-name-2": "why this role is needed",
            "excluded-role": "why this role is not needed"
          }
        }
        """;

    public TriageResult? Parse(string response, IReadOnlyList<RoleSkillDefinition> availableRoles)
    {
        try
        {
            var json = response;
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
                json = response[jsonStart..(jsonEnd + 1)];

            var parsed = JsonSerializer.Deserialize<TriageResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed is null) return null;

            var validNames = availableRoles.Select(r => r.Name).ToHashSet();
            var validParticipants = parsed.Participants
                .Where(p => validNames.Contains(p))
                .ToList();

            if (validParticipants.Count == 0) return null;

            var lead = validNames.Contains(parsed.Lead) ? parsed.Lead : validParticipants[0];

            return new TriageResult { Lead = lead, Participants = validParticipants, Reasoning = parsed.Reasoning };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse triage response");
            return null;
        }
    }
}

internal class TriageResult
{
    public string Lead { get; set; } = string.Empty;
    public List<string> Participants { get; set; } = [];
    public Dictionary<string, string> Reasoning { get; set; } = new();
}
