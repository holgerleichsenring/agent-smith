using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Dialogue;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-26-bdd0g: derive_specs — the person asked in the ticket's conversation for its specs to be derived and
/// reviewed without starting an attempt. Offered only to the master of a bound turn whose poster may start runs.
/// </summary>
public sealed class DeriveSpecsToolHost(ISpecDerivationStart start) : IToolHost
{
    public const string Name = "derive_specs";
    public const string Preview = "preview";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(Derive, name: Name)];
    }

    [Description(
        "Derive this ticket's specs and review them, without starting an attempt, because the person in this "
        + "conversation asked for it. Call it only on the person's word. The ticket's status does not change. "
        + "Mode 'branch' stores the specs on the ticket branch with a draft spec pull request, so the next attempt "
        + "builds from them; mode 'preview' writes nothing and shows the specs on the ticket page. The answer is "
        + "'started' with the run, or the reason nothing was started.")]
    private async Task<string> Derive(
        [Description("'branch' (the default) or 'preview'.")] string mode,
        [Description("What the person asked for, in one sentence.")] string reason,
        CancellationToken cancellationToken) =>
        await start.StartAsync(string.Equals(mode, Preview, StringComparison.OrdinalIgnoreCase), reason, cancellationToken);
}
