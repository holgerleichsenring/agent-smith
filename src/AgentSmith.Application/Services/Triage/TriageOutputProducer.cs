using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Contracts.Activation;
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
    ActivationSkillFilter activationFilter,
    PhaseSpecificityTrimmer phaseTrimmer,
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    LoopLimitsConfig limits,
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
        var skills = LoadAvailableSkills(pipeline);
        var filtered = ApplyActivatesWhenFilter(pipeline, skills);
        var input = BuildInput(pipeline, filtered);
        var agent = pipeline.Get<AgentConfig>(ContextKeys.AgentConfig);
        var vocabulary = pipeline.TryGet<ConceptVocabulary>(ContextKeys.ConceptVocabulary, out var loaded)
                         && loaded is not null
            ? loaded
            : ConceptVocabulary.Empty;
        var output = await CallAndValidateAsync(input, agent, vocabulary, retry: false, cancellationToken)
                     ?? await CallAndValidateAsync(input, agent, vocabulary, retry: true, cancellationToken)
                     ?? throw new InvalidOperationException("Triage output failed validation after retry");
        var trimmed = phaseTrimmer.Trim(output, filtered, limits.MaxSkillsPerPhase);
        return labelOverrider.Apply(trimmed, input.TicketLabels);
    }

    private IReadOnlyList<RoleSkillDefinition> ApplyActivatesWhenFilter(
        PipelineContext pipeline, IReadOnlyList<RoleSkillDefinition> skills)
    {
        var filtered = activationFilter.Filter(skills, conceptsFactory(pipeline));
        logger.LogInformation(
            "Triage filtered {Before}->{After} skills via activates_when", skills.Count, filtered.Count);
        return filtered;
    }

    private TriageInput BuildInput(
        PipelineContext pipeline, IReadOnlyList<RoleSkillDefinition> skills)
    {
        var (ticketText, labels) = ResolveTicketOrSyntheticInput(pipeline);
        var excerpt = excerptBuilder.Build(pipeline);
        var skillIndex = skills.Where(r => r.Role is not null).Select(ToIndexEntry).ToList();
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

    private static IReadOnlyList<RoleSkillDefinition> LoadAvailableSkills(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null)
            return Array.Empty<RoleSkillDefinition>();
        return roles;
    }

    private static SkillIndexEntry ToIndexEntry(RoleSkillDefinition skill) => new(
        skill.Name,
        skill.Description,
        skill.Role!, // null-checked by caller's `Where(r => r.Role is not null)`
        skill.OutputSchema,
        skill.ActivatesWhen);

    private async Task<TriageOutput?> CallAndValidateAsync(
        TriageInput input, AgentConfig agent, ConceptVocabulary vocabulary,
        bool retry, CancellationToken cancellationToken)
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
        var validation = validator.Validate(parsed, input.AvailableSkills, vocabulary);
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
            var activation = string.IsNullOrWhiteSpace(s.ActivatesWhen) ? "(always)" : s.ActivatesWhen;
            return $"- {s.Name} (role: {s.Role}; activates_when: {activation}): {s.Description}";
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
