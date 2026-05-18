using System.Diagnostics.Metrics;
using AgentSmith.Application.Services.Metrics;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0140e: small helper around <see cref="MeterListener"/> for unit-testing
/// counter increments on the static <see cref="AgentSmithMeter"/>. Subscribes
/// to a single named instrument on the "AgentSmith" meter and exposes the
/// captured measurements + tag arrays for assertions.
///
/// Counters are static so all tests share the underlying meter. Tests should
/// only assert on the count + tags of measurements captured DURING their own
/// action — entries from other (concurrent) tests are filtered out implicitly
/// because each listener instance only sees events published after its
/// <see cref="MeterListener.Start"/> and before its disposal.
/// </summary>
internal sealed class MeterCapture : IDisposable
{
    private readonly MeterListener _listener;
    private readonly List<(long Value, KeyValuePair<string, object?>[] Tags)> _measurements = new();
    private readonly object _lock = new();

    public IReadOnlyList<(long Value, KeyValuePair<string, object?>[] Tags)> Measurements
    {
        get { lock (_lock) return _measurements.ToList(); }
    }

    private MeterCapture(string instrumentName)
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == AgentSmithMeter.MeterName
                    && instrument.Name == instrumentName)
                    l.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            lock (_lock) _measurements.Add((value, tags.ToArray()));
        });
        _listener.Start();
    }

    /// <summary>
    /// Starts capturing measurements for a single counter on the AgentSmith meter.
    /// Call inside a using-statement; dispose unsubscribes the listener so other
    /// tests aren't affected.
    /// </summary>
    public static MeterCapture ForCounter(string instrumentName) => new(instrumentName);

    public void Dispose()
    {
        _listener.RecordObservableInstruments(); // flush any pending observable instruments
        _listener.Dispose();
    }
}
