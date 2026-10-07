using System.Text;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-07-6b9db: one stream of a step's live output — its head up to the 1 MB buffer, then a
/// rolling tail of its last lines, and a count of every character it carried.
/// <para>
/// The buffer used to keep the head only, and one flag shared by both streams stopped stdout and
/// stderr together: jest writing a megabyte to stderr left the model neither stderr's summary
/// nor any later stdout. A build's reason sits at the END of its output (p0419), so the tail is
/// kept after the head fills. Memory stays bounded: the head at the buffer, the tail at
/// <see cref="TailMaxChars"/>.
/// </para>
/// </summary>
internal sealed class StreamCapture
{
    // Wide enough for the tail half of any section budget a render cuts this stream to.
    public const int TailMaxChars = SizeLimits.RunCommandStdoutMaxChars;

    private const string TruncationNotice = "\n... (output truncated at 1 MB)";

    private readonly StringBuilder _head = new();
    private readonly Queue<string> _tail = new();
    private int _tailChars;

    /// <summary>True once the head is full and later lines go to the rolling tail.</summary>
    public bool Truncated { get; private set; }

    /// <summary>Every character the stream carried, one newline per line — the way the agent
    /// writes the same lines into the result body.</summary>
    public long Total { get; private set; }

    public string Head => _head.ToString();
    public string Tail => string.Concat(_tail);

    /// <summary>The head-only text the program render (find_files, http_request) has always
    /// read, with the 1 MB notice when the head filled.</summary>
    public string Text => Truncated ? Head + TruncationNotice : Head;

    public void Append(string line)
    {
        Total += line.Length + 1;
        if (!Truncated && _head.Length + Encoding.UTF8.GetByteCount(line) + 1 <= SizeLimits.RunCommandMaxBufferBytes)
        {
            _head.Append(line).Append('\n');
            return;
        }
        Truncated = true;
        AppendToTail(line + "\n");
    }

    /// <summary>
    /// The held characters that come after <paramref name="position"/> of the stream: the end of
    /// the head while it holds everything, otherwise the part of the rolling tail past it.
    /// </summary>
    public string After(long position)
    {
        if (!Truncated) return position < _head.Length ? Head[(int)position..] : string.Empty;
        var tail = Tail;
        var tailStart = Total - tail.Length;
        return position <= tailStart ? tail : tail[(int)Math.Min(tail.Length, position - tailStart)..];
    }

    private void AppendToTail(string line)
    {
        // A single line wider than the tail keeps its own last characters.
        if (line.Length > TailMaxChars) line = line[^TailMaxChars..];
        _tail.Enqueue(line);
        _tailChars += line.Length;
        while (_tailChars > TailMaxChars) _tailChars -= _tail.Dequeue().Length;
    }
}
