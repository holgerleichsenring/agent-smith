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
            "a handler that can REACH a chat client runs a model on some path; declare it in "
            + "CommandModelUse, or make the reach impossible. Paths:\n  "
            + string.Join("\n  ", undeclared.Select(c => PathToChatClient(
                Assembly.GetType($"AgentSmith.Application.Services.Handlers.{c.Replace("Command", "Handler", StringComparison.Ordinal)}")
                ?? Handlers().First(h => h.Name == c.Replace("Command", "Handler", StringComparison.Ordinal))))));
    }

    /// <summary>
    /// p0433: the rule that would have caught the miss, named after the shape that caused
    /// it. A synthetic chain of concrete types proves nothing here — the walk only earns
    /// its keep if it crosses the interface hop, so the case IS VerifyPhase.
    /// </summary>
    [Fact]
    public void ModelUse_TheRuleWouldHaveCaughtVerifyPhase()
    {
        var handler = typeof(VerifyPhaseHandler);

        handler.GetConstructors().SelectMany(c => c.GetParameters())
            .Should().NotContain(p => p.ParameterType == typeof(IChatClientFactory),
                "the point of this case is that the chat client is NOT on the handler");
        ReachesAChatClient(handler).Should().BeTrue(
            "VerifyPhaseHandler -> PhaseAccounting -> ISpecAccountant -> SpecAccountant, "
            + "and the middle hop is an interface");
    }

    [Fact]
    public void VerifyPhase_IsDeclaredAsAModelStep()
    {
        var step = CommandModelUse.For(CommandNames.VerifyPhase);

        step.Use.Should().Be(ModelUse.Call,
            "the step that decides whether a run delivered asks a model to account for "
            + "every ratified criterion");
        step.Answer.Should().NotBeNullOrWhiteSpace();
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

    // p0433: handlers that can REACH a chat client, not only those holding one. p0408
    // stopped at the handler's own constructor because "services in between are out of
    // reach for a reflection check" — and the step that decides delivery then sat two
    // hops away and was drawn as machinery on the website for two phases.
    //
    // The walk has to cross an INTERFACE at the hop that matters:
    //   VerifyPhaseHandler(PhaseAccounting) -> PhaseAccounting(ISpecAccountant)
    //   -> SpecAccountant(IChatClientFactory)
    // An interface declares no constructor, so a walk over parameter types alone reaches
    // nothing and would report the step clean — a false negative wearing the evidence's
    // clothes. So an interface expands to every implementation in the assembly. That
    // over-reports where several exist, which is the safe direction for a cross-check
    // whose verdict is "explain this", not "this is what happens".
    private static IEnumerable<Type> ChatClientHandlers() =>
        Handlers().Where(ReachesAChatClient);

    private static IEnumerable<Type> Handlers() =>
        Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true }
                && t.Name.EndsWith("Handler", StringComparison.Ordinal));

    internal static bool ReachesAChatClient(Type root) => PathToChatClient(root) is not null;

    /// <summary>
    /// p0433: the PATH, not just the verdict. A rule that says "this reaches a model"
    /// without saying how sends the reader hunting through three service constructors —
    /// and the whole reason this drifted is that nobody was going to do that.
    /// </summary>
    internal static string? PathToChatClient(Type root)
    {
        var seen = new HashSet<Type>();
        var pending = new Stack<IReadOnlyList<Type>>([[root]]);
        while (pending.Count > 0)
        {
            var path = pending.Pop();
            var type = path[^1];
            if (!seen.Add(type)) continue;
            if (type == typeof(IChatClientFactory) || type == typeof(IChatClient))
                return string.Join(" -> ", path.Select(t => t.Name));
            foreach (var next in DependenciesOf(type)) pending.Push([.. path, next]);
        }
        return null;
    }

    private static IEnumerable<Type> DependenciesOf(Type type)
    {
        if (type.IsInterface) return Implementations(type);
        if (type.Assembly != Assembly && type.Assembly != typeof(CommandModelUse).Assembly) return [];
        return type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);
    }

    private static Type[] Implementations(Type contract) =>
        [.. Assembly.GetTypes().Where(t => t is { IsAbstract: false, IsClass: true }
            && contract.IsAssignableFrom(t))];

    private static Assembly Assembly => typeof(AgenticMasterHandler).Assembly;
}
