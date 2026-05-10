using AgentSmith.Application.Services.Loop;

namespace AgentSmith.Application.Services.Validation;

/// <summary>
/// Resolves the ISkillOutputValidator for a skill's declared output_schema.
/// Null/empty input means the legacy SKILL.md path that has not declared a schema —
/// returns NoOpSkillOutputValidator. An unknown non-empty value is a config bug
/// (NewFormatSkillValidator already gates the closed enum at boot) — throws.
/// </summary>
public sealed class SkillOutputValidatorFactory
{
    private readonly Dictionary<SkillOutputSchema, ISkillOutputValidator> _validators;
    private readonly NoOpSkillOutputValidator _noOp;

    public SkillOutputValidatorFactory(
        PlanOutputValidator plan,
        DiffOutputValidator diff,
        BootstrapOutputValidator bootstrap,
        ObservationOutputValidator observation,
        NoOpSkillOutputValidator noOp)
    {
        _validators = new Dictionary<SkillOutputSchema, ISkillOutputValidator>
        {
            [SkillOutputSchema.Plan] = plan,
            [SkillOutputSchema.Diff] = diff,
            [SkillOutputSchema.Bootstrap] = bootstrap,
            [SkillOutputSchema.Observation] = observation
        };
        _noOp = noOp;
    }

    /// <summary>Test-only ctor: every schema resolves to the same validator. Lets test
    /// fixtures avoid constructing the four real validators when they only care about
    /// the runtime composition path.</summary>
    internal SkillOutputValidatorFactory(ISkillOutputValidator @default, NoOpSkillOutputValidator noOp)
    {
        _validators = new Dictionary<SkillOutputSchema, ISkillOutputValidator>
        {
            [SkillOutputSchema.Plan] = @default,
            [SkillOutputSchema.Diff] = @default,
            [SkillOutputSchema.Bootstrap] = @default,
            [SkillOutputSchema.Observation] = @default
        };
        _noOp = noOp;
    }

    public ISkillOutputValidator ForSchema(string? outputSchema)
    {
        if (string.IsNullOrWhiteSpace(outputSchema)) return _noOp;
        return _validators[Parse(outputSchema)];
    }

    private static SkillOutputSchema Parse(string raw) => raw switch
    {
        "plan" => SkillOutputSchema.Plan,
        "diff" => SkillOutputSchema.Diff,
        "bootstrap" => SkillOutputSchema.Bootstrap,
        "observation" => SkillOutputSchema.Observation,
        _ => throw new ArgumentException(
            $"unknown output_schema '{raw}' (expected plan/diff/bootstrap/observation)",
            nameof(raw))
    };
}
