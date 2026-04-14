namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Prompt templates and string constants for .context.yaml generation.
/// </summary>
internal static class ContextPromptTemplates
{
    internal const string SystemPrompt = """
        You are a project analyst. Generate a .context.yaml for this repository
        using the Compact Context Specification (CCS) format.

        Rules:
        - Use ONLY the template format provided
        - Fill in what you can determine from the files and structure
        - Leave out optional sections you cannot determine
        - Be precise, not verbose
        - meta.purpose must be one sentence, max 100 characters
        - Return ONLY valid YAML, no explanation or markdown fences
        """;

    internal const string QualityTemplate = """
            quality:
              lang: english-only
              principles: [<detected-principles>]
              detected-style:
                naming: { classes: <PascalCase|camelCase|snake_case>, variables: <camelCase|snake_case>, files: <pattern> }
                indentation: { type: <spaces|tabs>, size: <n> }
                formatter: <name-or-none>
                linter: <name-or-none>
                pre-commit: <name-or-none>
              architecture:
                style: [<DDD|CleanArch|Hexagonal|MVC|Layered|ad-hoc>]
                patterns: [<CQRS|Repository|MediatR|Factory|Strategy|etc>]
                layer-discipline: <strict|loose|none>
                domain-model: <rich|anemic|none>
                di-approach: <constructor-injection|service-locator|framework-managed|none>
              methodology:
                testing: <test-first|test-after|no-tests>
                test-style: <AAA|GWT|BDD|unclear>
                coverage-estimate: <high|medium|low|none>
                ci-enforced: <true|false>
              quality-score: <high|medium|low>
              recommendation: <follow-existing|suggest-improvements>
            """;
}
