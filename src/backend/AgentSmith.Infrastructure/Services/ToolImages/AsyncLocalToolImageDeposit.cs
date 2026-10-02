using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.ToolImages;

/// <summary>
/// 2026-10-01-283dd: <see cref="IToolImageDeposit"/> over the ambient loop frame. The tool and
/// the call id come from the invocation that is running, so a producer states only the image.
/// </summary>
public sealed class AsyncLocalToolImageDeposit(ToolImageRule rule, ToolImageLoopFrames frames)
    : IToolImageDeposit
{
    public const string NoLoopRefusal =
        "no tool call of an open tool loop is running, so there is no conversation to show the image in";

    public ToolImageDepositResult Deposit(ToolImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var call = FunctionInvokingChatClient.CurrentContext;
        var frame = frames.Current;
        if (call is null || frame is null)
            return ToolImageDepositResult.Refused(NoLoopRefusal);
        var refusal = rule.Refusal(image)
            ?? frame.Add(new DepositedToolImage(image, call.Function.Name, call.CallContent.CallId));
        return refusal is null ? ToolImageDepositResult.Accepted : ToolImageDepositResult.Refused(refusal);
    }
}
