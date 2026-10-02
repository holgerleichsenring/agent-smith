using AgentSmith.Infrastructure.Models;

namespace AgentSmith.Infrastructure.Services.ToolImages;

/// <summary>
/// 2026-10-01-283dd: one tool loop's images — those deposited since the last tool round and how
/// many the loop has already shown. Tools may run concurrently, so every access is locked.
/// </summary>
public sealed class ToolImageLoopFrame
{
    public const int MaxPerCall = 2;
    public const int MaxPerLoop = 4;

    private readonly object _gate = new();
    private readonly List<DepositedToolImage> _pending = [];
    private readonly Dictionary<string, int> _perCall = new(StringComparer.Ordinal);
    private int _shown;

    /// <summary>Queues the image; null when taken, otherwise why not.</summary>
    public string? Add(DepositedToolImage image)
    {
        lock (_gate)
        {
            var count = _perCall.GetValueOrDefault(image.CallId);
            if (count >= MaxPerCall)
                return $"a tool call may deposit at most {MaxPerCall} images";
            _perCall[image.CallId] = count + 1;
            _pending.Add(image);
            return null;
        }
    }

    /// <summary>Hands out everything deposited since the last call and forgets it.</summary>
    public IReadOnlyList<DepositedToolImage> TakePending()
    {
        lock (_gate)
        {
            var taken = _pending.ToList();
            _pending.Clear();
            return taken;
        }
    }

    /// <summary>Grants up to <paramref name="wanted"/> of the loop's remaining showings.</summary>
    public int ReserveShowings(int wanted)
    {
        lock (_gate)
        {
            var granted = Math.Clamp(MaxPerLoop - _shown, 0, wanted);
            _shown += granted;
            return granted;
        }
    }
}
