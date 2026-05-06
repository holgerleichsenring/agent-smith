using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Builds a TriageInput from pipeline state, calls the LLM with the structured triage prompt,
/// parses single-line JSON, validates, and applies ticket-label overrides. One retry on
/// validation failure with a stricter system reminder; second failure throws.
/// </summary>
public sealed class TriageOutputProducer(
    ProjectMapExcerptBuilder excerptBuilder,
    TriageOutputValidator validator,
    TriageLabelOverrideApplier labelOverrider,
    IPromptCatalog prompts,
    IChatClientFactory chatClientFactory,
    ILogger<TriageOutputProducer> logger) : ITriageOutputProducer
{
    private static readonly IReadOnlyList<PipelinePhase> DefaultPhases =
        [PipelinePhase.Plan, PipelinePhase.Review, PipelinePhase.Final];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<TriageOutput> ProduceAsync(
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var input = BuildInput(pipeline);
        var agent = pipeline.Get<AgentConfig>(ContextKeys.AgentConfig);
        var output = await CallAndValidateAsync(input, agent, retry: false, cancellationToken)
                     ?? await CallAndValidateAsync(input, agent, retry: true, cancellationToken)
                     ?? throw new InvalidOperationException("Triage output failed validation after retry");
        return labelOverrider.Apply(output, input.TicketLabels);
    }

    private TriageInput BuildInput(PipelineContext pipeline)
    {
        var (ticketText, labels) = ResolveTicketOrSyntheticInput(pipeline);
        var excerpt = excerptBuilder.Build(pipeline);
        var skillIndex = LoadSkillIndex(pipeline);
        return new TriageInput(
            Ticket: ticketText,
            ProjectMapExcerpt: excerpt,
            AvailableSkills: skillIndex,
            Phases: DefaultPhases,
            TicketLabels: labels);
    }

    internal static (string Ticket, IReadOnlyList<string> Labels) ResolveTicketOrSyntheticInput(
        PipelineContext pipeline)
    {
        if (pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) && ticket is not null)
        {
            return ($"{ticket.Title}\n\n{ticket.Description}", ticket.Labels);
        }

        var synthetic = pipeline.TryGet<object>(ContextKeys.SwaggerSpec, out _)
            ? "API security scan. No ticket context — triage based on the project map and available scan skills."
            : "Security scan. No ticket context — triage based on the project map and available scan skills.";
        return (synthetic, Array.Empty<string>());
    }

    private static IReadOnlyList<SkillIndexEntry> LoadSkillIndex(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null)
            return Array.Empty<SkillIndexEntry>();
        return roles.Where(r => r.RolesSupported is not null).Select(ToIndexEntry).ToList();
    }

    private static SkillIndexEntry ToIndexEntry(RoleSkillDefinition skill) => new(
        skill.Name,
        skill.Description,
        skill.RolesSupported ?? [],
        skill.Activation ?? ActivationCriteria.Empty,
        skill.RoleAssignments ?? [],
        skill.OutputContract?.OutputType ?? new Dictionary<SkillRole, OutputForm>());

    private async Task<TriageOutput?> CallAndValidateAsync(
        TriageInput input, AgentConfig agent, bool retry, CancellationToken cancellationToken)
    {
        var system = retry ? AddStrictReminder(prompts.Get("triage-structured-system")) : prompts.Get("triage-structured-system");
        var user = prompts.Render("triage-structured-user", BuildTokens(input));
        var chat = chatClientFactory.Create(agent, TaskType.Reasoning);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(agent, TaskType.Reasoning);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, system),
            new(ChatRole.User, user),
        };
        var response = await chat.GetResponseAsync(messages,
            new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
        var parsed = ParseJson(response.Text ?? string.Empty);
        if (parsed is null)
        {
            logger.LogWarning("Triage output JSON parse failed (retry={Retry})", retry);
            return null;
        }
        var validation = validator.Validate(parsed, input.AvailableSkills);
        if (validation.IsValid) return parsed;
        logger.LogWarning("Triage output validation failed (retry={Retry}): {Errors}",
            retry, string.Join("; ", validation.Errors));
        return null;
    }

    private static IReadOnlyDictionary<string, string> BuildTokens(TriageInput input) =>
        new Dictionary<string, string>
        {
            ["ticket"] = input.Ticket,
            ["project_map_excerpt"] = RenderExcerpt(input.ProjectMapExcerpt),
            ["labels"] = input.TicketLabels.Count > 0 ? string.Join(", ", input.TicketLabels) : "(none)",
            ["phases"] = string.Join(", ", input.Phases),
            ["available_skills"] = RenderSkills(input.AvailableSkills),
        };

    private static string RenderExcerpt(ProjectMapExcerpt excerpt)
    {
        var stack = excerpt.Stack.Count > 0 ? string.Join(", ", excerpt.Stack) : "(none)";
        var concepts = excerpt.Concepts.Count > 0 ? string.Join(", ", excerpt.Concepts) : "(none)";
        return $"type: {excerpt.Type}\nstack: {stack}\nconcepts: {concepts}\n" +
               $"test_capability: hasSetup={excerpt.TestCapability.HasTestSetup}, runnable={excerpt.TestCapability.RunnableInPipeline}\n" +
               $"ci_capability: hasPipeline={excerpt.CiCapability.HasPipeline}";
    }

    private static string RenderSkills(IReadOnlyList<SkillIndexEntry> skills)
    {
        if (skills.Count == 0) return "(none)";
        var lines = skills.Select(s =>
        {
            var roles = string.Join(",", s.RolesSupported.Select(r => r.ToString().ToLowerInvariant()));
            var positive = string.Join(",", s.Activation.Positive.Select(k => k.Key));
            return $"- {s.Name} (roles: {roles}; positive: {positive}): {s.Description}";
        });
        return string.Join("\n", lines);
    }

    private static TriageOutput? ParseJson(string text)
    {
        var json = ExtractJsonObject(text);
        if (json is null) return null;
        try
        {
            return JsonSerializer.Deserialize<TriageOutputDto>(json, JsonOptions)?.ToOutput();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        return text[start..(end + 1)];
    }

    private static string AddStrictReminder(string system) => system + """


        ## STRICT REPROMPT

        Your previous response failed validation. Re-read the rules above carefully.
        Single-line JSON only. Use only declared activation/role_assignment keys.
        Roles must be in each cited skill's `roles_supported`. No newlines in the JSON.
        """;
}

internal sealed record TriageOutputDto(
    Dictionary<PipelinePhase, PhaseAssignment>? Phases,
    int Confidence,
    string? Rationale)
{
    public TriageOutput ToOutput() => new(
        Phases ?? new Dictionary<PipelinePhase, PhaseAssignment>(),
        Confidence,
        Rationale ?? string.Empty);
}
