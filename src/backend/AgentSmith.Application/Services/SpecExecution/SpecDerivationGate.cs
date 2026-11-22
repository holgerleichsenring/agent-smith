using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>
/// 2026-11-19-6b0ba: SpecGate's word in a spec derivation, where the coding master writes each stand-in and builds
/// nothing. The front gate lets the sequence run while a stand-in waits and ends the run when none does; the gate
/// spliced after a master judges only that master's spec — a question it asked with ask_human (96c8a) becomes the hand-back
/// step asks on the ticket, a stand-in it did not write fails the run, and once no stand-in is left the run ends on
/// where its specs are. A stand-in deferred to a later attempt is not judged here.
/// </summary>
public static class SpecDerivationGate
{
    /// <summary>The front gate: null lets the sequence run, else the run ends on its sentence.</summary>
    public static CommandResult? Front(PipelineContext pipeline, SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(set);
        if (set.UnexecutedTail.Any(p => set.IsStandIn(p.SpecId))) return null;
        return End(pipeline, set);
    }

    /// <summary>The gate after the master that wrote <paramref name="specId"/>.</summary>
    public static CommandResult AfterMaster(PipelineContext pipeline, SpecSet set, string specId)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(set);
        // 2026-11-22-96c8a: the question the master asked with ask_human is the hand-back's.
        if (pipeline.TryGet<IReadOnlyList<PlanOpenQuestion>>(ContextKeys.MasterOpenQuestions, out var asked)
            && asked is [{ Question: { Length: > 0 } question }, ..])
        {
            pipeline.Remove(ContextKeys.MasterOpenQuestions);
            pipeline.Set(ContextKeys.SpecHandback, new SpecHandback(SpecHandbackCase.Question, question));
            return CommandResult.OkAndContinueWith(
                $"Spec {specId} needs an answer first: {question}", new PipelineCommand(CommandNames.SpecHandback));
        }
        if (set.IsStandIn(specId))
            return CommandResult.Fail($"The coding master wrote no spec for {specId}: it is still the ticket as it came in. "
                + (FirstLine(pipeline) is { } said ? $"It reported: {said}" : "It reported nothing."));
        return set.UnexecutedTail.Any(p => set.IsStandIn(p.SpecId))
            ? CommandResult.Ok($"Spec {specId} written: {set.Specs.First(p => p.SpecId == specId).Draft.Goal}")
            : End(pipeline, set);
    }

    private static string? FirstLine(PipelineContext pipeline) =>
        pipeline.TryGet<MasterVerification>(ContextKeys.MasterVerification, out var verified)
            ? verified?.Report?.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) : null;

    private static CommandResult End(PipelineContext pipeline, SpecSet set)
    {
        var ended = SpecDerivationSummary.Of(pipeline, set);
        pipeline.Set(ContextKeys.SpecDerivationEnded, ended);
        return CommandResult.Ok(ended);
    }
}
