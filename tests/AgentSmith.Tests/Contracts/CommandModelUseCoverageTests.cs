using System.Reflection;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.ContractsCoverage;

/// <summary>
/// p0408: keeps the model-use declaration honest. It is a declaration because the chat
/// client reaches most handlers through two or three services — so instead of inferring,
/// these tests fail when a preset gains an unclassified step, and when a handler starts
/// calling a model without saying so.
/// </summary>
public sealed class CommandModelUseCoverageTests
{
    [Fact]
    public void ModelUse_EveryEffectivePresetStep_IsClassified()
    {
        var missing = PipelinePresets.Names
            .SelectMany(PipelinePresets.Effective)
            .Distinct(StringComparer.Ordinal)
            .Where(command => !CommandModelUse.IsClassified(command))
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        missing.Should().BeEmpty(
            "every step a preset executes must say whether a model runs there — the generated "
            + "control-flow diagram draws unclassified steps as deterministic. Unclassified: "
            + string.Join(", ", missing));
    }

    [Fact]
    public void ModelUse_HandlerTakingAChatClient_IsDeclaredAsAModelStep()
    {
        var live = PipelinePresets.Names
            .SelectMany(PipelinePresets.Effective)
            .ToHashSet(StringComparer.Ordinal);

        var undeclared = ChatClientHandlers()
            .Select(t => t.Name.Replace("Handler", "Command", StringComparison.Ordinal))
            .Where(command => live.Contains(command))
            .Where(command => CommandModelUse.For(command).Use == ModelUse.None)
            .ToList();

        undeclared.Should().BeEmpty(
            "a handler that constructs a chat client runs a model; declare it in CommandModelUse. "
            + string.Join(", ", undeclared));
    }

    [Fact]
    public void ModelUse_EveryModelStep_NamesWhatItAsksFor()
    {
        foreach (var (command, step) in CommandModelUse.ModelSteps)
        {
            step.Use.Should().NotBe(ModelUse.None, $"'{command}' is listed as a model step");
            step.Answer.Should().NotBeNullOrWhiteSpace(
                $"'{command}' must say what agent-smith expects back — that is the steering");
        }
    }

    [Fact]
    public void ModelUse_TheMasterLoopStep_TakesItsActorFromThePipeline()
    {
        var master = CommandModelUse.For(CommandNames.AgenticMaster);

        master.Use.Should().Be(ModelUse.Loop);
        master.Actor.Should().BeEmpty("the master is per pipeline, from PipelinePresets.MasterFor");
    }

    // Handlers whose own constructor takes the chat client (or its factory). Services in
    // between are out of reach for a reflection check, which is why this is a safety net
    // for the obvious case and not a substitute for the declaration.
    private static IEnumerable<Type> ChatClientHandlers() =>
        typeof(AgenticMasterHandler).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && t.Name.EndsWith("Handler", StringComparison.Ordinal))
            .Where(t => t.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Any(p => p.ParameterType == typeof(IChatClientFactory) || p.ParameterType == typeof(IChatClient)));
}
