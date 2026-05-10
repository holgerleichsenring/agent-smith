using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Commands;

/// <summary>
/// Build-time / CI verb that validates the concept vocabulary against (a) every
/// <c>activates_when</c> expression in skill SKILL.md files (parsed via p0125b)
/// and (b) the registered <see cref="Contracts.Activation.IConceptWriter"/>
/// instances. Exits 0 on a clean tree; exits 1 with a list of <c>(subject,
/// concept, error)</c> tuples on any inconsistency. Operators see the full
/// picture, not a fix-and-rerun loop.
/// </summary>
public sealed class ValidateConceptsCommand(
    ConceptVocabularyLoader vocabularyLoader,
    ISkillLoader skillLoader,
    ActivationExpressionParser parser,
    ConceptWriterRegistry registry)
{
    public ConceptValidationResult Validate(string skillsDirectory)
    {
        var errors = new List<ConceptValidationError>();
        var vocabulary = vocabularyLoader.Load(skillsDirectory);
        var skills = skillLoader.LoadRoleDefinitions(skillsDirectory);

        foreach (var skill in skills)
        {
            CheckActivatesWhen(skill, vocabulary, errors);
            CheckNewFormatRules(skill, errors);
        }

        foreach (var (conceptName, handlers) in registry.ConceptToHandlers)
            CheckHandlerSide(conceptName, handlers, vocabulary, errors);

        foreach (var concept in vocabulary.Concepts.Values)
            CheckVocabularySide(concept, errors);

        var sorted = errors.OrderBy(e => e.Subject, StringComparer.Ordinal)
            .ThenBy(e => e.Concept, StringComparer.Ordinal).ToList();
        return new ConceptValidationResult(sorted, sorted.Count == 0 ? 0 : 1);
    }

    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var skillsPathOption = new Option<string>(
            "--skills-path",
            "Skills directory containing SKILL.md files and concept-vocabulary.yaml") { IsRequired = true };
        var cmd = new Command(
            "validate-concepts",
            "Validate skill activates_when expressions and IConceptWriter consistency against the concept vocabulary")
        {
            skillsPathOption, configOption, verboseOption,
        };
        cmd.SetHandler((InvocationContext ctx) => RunHandler(ctx, skillsPathOption, verboseOption));
        return cmd;
    }

    private static void RunHandler(
        InvocationContext ctx, Option<string> skillsPathOption, Option<bool> verboseOption)
    {
        var skillsPath = ctx.ParseResult.GetValueForOption(skillsPathOption)!;
        var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
        using var provider = ServiceProviderFactory.Build(verbose, headless: true, string.Empty, string.Empty);
        var validator = provider.GetRequiredService<ValidateConceptsCommand>();
        var result = validator.Validate(skillsPath);
        foreach (var error in result.Errors)
            Console.WriteLine($"{error.Subject}: {error.Concept}: {error.Message}");
        if (result.ExitCode == 0)
            Console.WriteLine("validate-concepts: OK");
        ctx.ExitCode = result.ExitCode;
    }

    private void CheckActivatesWhen(
        RoleSkillDefinition skill, ConceptVocabulary vocabulary, List<ConceptValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(skill.ActivatesWhen)) return;
        ActivationExpression ast;
        try { ast = parser.Parse(skill.ActivatesWhen); }
        catch (ActivationExpressionParseException ex)
        {
            errors.Add(new ConceptValidationError(skill.Name, "<parse>",
                $"activates_when parse error at offset {ex.Offset} (offending token: '{ex.OffendingToken}'): {ex.Message}"));
            return;
        }
        foreach (var name in CollectIdentifiers(ast).Distinct(StringComparer.Ordinal))
            if (!vocabulary.TryGet(name, out _))
                errors.Add(new ConceptValidationError(
                    skill.Name, name, "activates_when references concept not declared in vocabulary"));
    }

    private static readonly HashSet<string> AllowedCategories = new(StringComparer.Ordinal)
    {
        "auth", "injection", "secrets", "iam", "crypto", "headers", "inputs", "outputs",
    };

    private static void CheckNewFormatRules(
        RoleSkillDefinition skill, List<ConceptValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(skill.Role)) return;
        if (skill.InvestigatorMode == "verify_hint" && string.IsNullOrWhiteSpace(skill.Category))
            errors.Add(new ConceptValidationError(skill.Name, "category",
                "category is required when investigator_mode=verify_hint"));
        if (skill.Role == "judge" && string.IsNullOrWhiteSpace(skill.BlockCondition))
            errors.Add(new ConceptValidationError(skill.Name, "block_condition",
                "block_condition is required when role=judge"));
        if (skill.OutputSchema == "bootstrap" && skill.Role != "producer")
            errors.Add(new ConceptValidationError(skill.Name, "output_schema",
                $"output_schema=bootstrap requires role=producer; got role='{skill.Role}'"));
        if (!string.IsNullOrWhiteSpace(skill.Category) && !AllowedCategories.Contains(skill.Category))
            errors.Add(new ConceptValidationError(skill.Name, "category",
                $"category must be one of {{auth, injection, secrets, iam, crypto, headers, inputs, outputs}}; got '{skill.Category}'"));
    }

    private static void CheckHandlerSide(
        string conceptName, IReadOnlyList<HandlerConcept> handlers,
        ConceptVocabulary vocabulary, List<ConceptValidationError> errors)
    {
        foreach (var handler in handlers)
        {
            if (!vocabulary.TryGet(conceptName, out var declared))
            {
                errors.Add(new ConceptValidationError(handler.HandlerClassName, conceptName,
                    "handler declares concept not present in vocabulary"));
                continue;
            }
            if (declared.Type != handler.DeclaredType)
                errors.Add(new ConceptValidationError(handler.HandlerClassName, conceptName,
                    $"handler declares type {handler.DeclaredType} but vocabulary declares {declared.Type}"));
            else if (!declared.Writers.Contains(handler.HandlerClassName))
                errors.Add(new ConceptValidationError(handler.HandlerClassName, conceptName,
                    "handler declares concept but is not listed in vocabulary writers"));
        }
    }

    private void CheckVocabularySide(ProjectConcept concept, List<ConceptValidationError> errors)
    {
        foreach (var writerName in concept.Writers)
        {
            var backed = registry.ConceptToHandlers.TryGetValue(concept.Name, out var handlers)
                && handlers.Any(h => h.HandlerClassName == writerName);
            if (!backed)
                errors.Add(new ConceptValidationError(writerName, concept.Name,
                    "vocabulary writer not backed by a registered IConceptWriter"));
        }
    }

    private static IEnumerable<string> CollectIdentifiers(ActivationExpression expression)
    {
        switch (expression)
        {
            case IdentifierExpression id: yield return id.Name; break;
            case AndExpression a: foreach (var n in WalkBinary(a.Left, a.Right)) yield return n; break;
            case OrExpression o: foreach (var n in WalkBinary(o.Left, o.Right)) yield return n; break;
            case NotExpression n: foreach (var id in CollectIdentifiers(n.Inner)) yield return id; break;
            case ComparisonExpression c: foreach (var n in WalkBinary(c.Left, c.Right)) yield return n; break;
        }
    }

    private static IEnumerable<string> WalkBinary(ActivationExpression left, ActivationExpression right)
    {
        foreach (var n in CollectIdentifiers(left)) yield return n;
        foreach (var n in CollectIdentifiers(right)) yield return n;
    }
}
