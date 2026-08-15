namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// p0419: the last N characters a command produced, across BOTH streams.
/// <para>
/// A failing command has to say why, and stderr alone does not say it: build
/// tools (MSBuild, npm, cargo) write their diagnostics to STDOUT, so a failing
/// build surfaced an empty ErrorMessage and the run reported a red build with
/// a blank reason. The TAIL, not the head — the head of a build log is restore
/// chatter and the error summary is last.
/// </para>
/// <para>
/// The host has its own copy for the in-process sandbox: the agent ships as a
/// separate image a customer may build on their own base, so it stays free of
/// host references.
/// </para>
/// </summary>
internal sealed class OutputTail(int budgetChars)
{
    private readonly Queue<string> lines = new();
    private int length;

    public bool IsEmpty => lines.Count == 0;

    public void Append(string line)
    {
        lock (lines)
        {
            lines.Enqueue(line);
            length += line.Length + 1;
            while (length > budgetChars && lines.Count > 1)
                length -= lines.Dequeue().Length + 1;
        }
    }

    public override string ToString()
    {
        lock (lines) return string.Join('\n', lines).Trim();
    }
}
