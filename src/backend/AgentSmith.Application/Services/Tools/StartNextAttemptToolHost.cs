using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Dialogue;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// start_next_attempt — the person asked in the ticket's conversation for the next attempt. Offered only to the
/// master of a bound turn whose poster may start runs and whose ticket has an attempt; never to a sub-agent.
/// </summary>
public sealed class StartNextAttemptToolHost(IAttemptStart start) : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(StartNext, name: "start_next_attempt")];
    }

    [Description(
        "Start this ticket's next attempt because the person in this conversation asked for it — for example "
        + "'the registry is added, go on'. Call it only on the person's word. A series edit not yet worked starts "
        + "as its approval; otherwise the latest attempt runs again unchanged. The answer is 'requested' — the "
        + "attempt starts through the rework queue, which can still decline and posts why on the ticket — or the "
        + "reason nothing was requested. Never promise an attempt number.")]
    private async Task<string> StartNext(
        [Description("What the person said changed, in one sentence.")] string reason,
        CancellationToken cancellationToken) =>
        await start.StartNextAsync(reason, cancellationToken);
}
