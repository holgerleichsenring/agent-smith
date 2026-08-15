using AgentSmith.Contracts.Models.Workers;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: renders a <see cref="WorkerRequest"/> into the text handed to an external
/// agent CLI. The worker enters as the MODEL, not as an agent: it answers one call with
/// text or tool calls and stops. Anything else — using its own tools, doing the work
/// itself — bypasses the master loop, the ledger, the nudges and the acceptance gate,
/// which are the machinery a worker-driven run exists to exercise.
/// </summary>
public sealed class WorkerPromptRenderer(WorkerJsonFormat json)
{
    public string Render(WorkerRequest request) =>
        Instructions + Environment.NewLine + json.Serialize(request) + Environment.NewLine;

    private const string Instructions =
        """
        You are answering ONE model call for the agent-smith framework. You occupy the
        position the provider model occupies inside a running agent loop: the request
        below is verbatim what the provider would have received — the system prompt, the
        full conversation including previous tool calls and their results, and the tools
        you may call with their JSON schemas.

        Do not do the work yourself and do not use your own tools. Decide what the agent
        should do next, exactly as the model would, and answer with it.

        TO ACT, answer with ONE JSON object and nothing else:

          {"tool_calls": [{"name": "<tool>", "arguments": { ... }}, ...]}

        Ask for EVERYTHING you already know you need. The framework runs the whole list
        and hands you every result together, so five independent reads cost one round
        instead of five. Round trips are the run's wall clock: a 40-minute phase is
        rarely 40 minutes of work, it is 85 round trips of waiting.

        Split into separate answers only where you genuinely must: when what you ask for
        next DEPENDS on what a result says, or when a call changes state that a later
        call in the same list would then read.

        TO ANSWER rather than act, just write the answer. Your whole output IS the
        model's reply — no envelope, no fence, no preamble about what you are about to
        say. Whatever the request asks for (prose, a JSON document, a verdict) is what
        you write, exactly as the provider model would have returned it.

        Rules:
        - The framework executes the tool calls and calls you again with their results.
          Only the tools listed in "tools" exist; naming any other tool fails the run.
        - "arguments" must be an object satisfying that tool's "input_schema".
        - Never emit an empty envelope and then keep talking: an envelope means you are
          acting, plain output means you are answering. One or the other, never both.
        - If you cannot answer this call at all, emit {"error": "<why>"}.

        REQUEST
        """;
}
