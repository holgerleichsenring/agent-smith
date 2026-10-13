using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-ee83: the Run step that reads a chunk of files in one sandbox call. The paths travel
/// as positional arguments to 'sh -c', never inside the script text; each file becomes one line
/// '&lt;index&gt; &lt;base64&gt;' (base64 with its wrapping removed, which GNU, BSD and busybox
/// all accept), a file that would overrun the byte budget becomes 'skip &lt;index&gt;', and the
/// script ends with 'end &lt;count&gt;'. A file it cannot open gets no line at all.
/// </summary>
public sealed class StructureBatchScript
{
    /// <summary>Paths per step: a few hundred keep the command line far below any ARG_MAX.</summary>
    public const int ChunkSize = 500;

    /// <summary>What the script may print, kept under the capture cap with room for skip lines.</summary>
    public const int ByteBudget = SizeLimits.RunStepCapturedStdoutMaxChars - 100_000;

    private const int TimeoutSeconds = 120;

    // A line costs its payload plus index, space and newline; 16 covers any index a chunk has.
    private const string Script = """
        used=0; i=0
        for f in "$@"; do
          if [ -f "$f" ] && [ -r "$f" ]; then
            n=$(wc -c < "$f" | tr -d ' ')
            e=$(( (n + 2) / 3 * 4 + 16 ))
            if [ $((used + e)) -gt BUDGET ]; then
              echo "skip $i"; used=$((used + 16))
            else
              printf '%s ' "$i"; base64 < "$f" | tr -d '\n'; printf '\n'; used=$((used + e))
            fi
          fi
          i=$((i + 1))
        done
        echo "end $i"
        """;

    public Step For(IReadOnlyList<string> paths) => new(
        Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "sh",
        Args: ["-c", Script.Replace("BUDGET", ByteBudget.ToString()), "sh", .. paths],
        WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: TimeoutSeconds);
}
