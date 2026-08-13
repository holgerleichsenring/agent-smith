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

        Do not do the work yourself and do not use your own tools. Decide the single next
        step the agent should take, exactly as the model would, and answer with it.

        Answer with ONE JSON object and nothing else — no prose, no markdown fence:

          {"text": "...", "tool_calls": [{"name": "<tool>", "arguments": { ... }}]}

        Rules:
        - To act, put one or more entries in "tool_calls". The framework executes them and
          calls you again with the results. Only the tools listed in "tools" exist; naming
          any other tool fails the run.
        - "arguments" must be an object satisfying that tool's "input_schema".
        - To answer rather than act, set "text" and leave "tool_calls" out.
        - Set "error" instead if you cannot answer this call at all.

        REQUEST
        """;
}
