namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: thin extraction of the FunctionInvokingChatClient loop core out
/// of <c>AgenticExecuteHandler</c>. One method, one responsibility:
/// take a fully-built request (prompts + tools + identity) and run the
/// chat completion. The handler builds the request, the sub-agent runner
/// builds the request — both share this one loop. No handler-calls-handler.
/// </summary>
public interface IAgenticLoopRunner
{
    Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken);
}
