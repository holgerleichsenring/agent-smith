using System.Text;

namespace AgentSmith.Infrastructure.Services.Sandbox;

/// <summary>
/// p0419: the last N characters a command produced, across BOTH streams.
/// <para>
/// A failing command has to say why. Capturing stderr alone was not enough:
/// MSBuild, npm and cargo write their diagnostics to STDOUT, so a failing
/// `dotnet build` produced an empty ErrorMessage and the run reported a red
/// build with a blank reason — twice, in two repositories, in the same run.
/// </para>
/// <para>
/// The TAIL, not the head: the head of a build log is restore chatter and the
/// error summary is last. Interleaved, because that is the order a terminal
/// shows them and the order that makes a log readable.
/// </para>
/// </summary>
public sealed class OutputTail(int budgetChars)
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
