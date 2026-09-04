using AgentSmith.Contracts.Events;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0173c/p0283b, extracted by 2026-09-04-102b: announces that agentsmith.yml was read, at
/// most once per version of it. LoadConfig runs on every poll and every webhook — some thirty
/// call sites — so announcing each call floods the event stream and the log with a fact that
/// has not changed; the file's path, last-write-time and size decide whether it has.
/// <para>
/// Its own type because deciding WHEN a read is worth announcing changes for reasons that have
/// nothing to do with reading a configuration file, and the loader had grown past the length
/// the ratchet allows.
/// </para>
/// </summary>
public sealed class ConfigFileReadAnnouncer(ISystemEventPublisher systemEvents)
{
    private readonly Lock _gate = new();
    private (string Path, long Mtime, int Size)? _lastAnnounced;

    public void Announce(string path, int sizeBytes)
    {
        try
        {
            var key = (path, File.GetLastWriteTimeUtc(path).Ticks, sizeBytes);
            lock (_gate)
            {
                if (_lastAnnounced == key) return;
                _lastAnnounced = key;
            }
            _ = systemEvents.PublishAsync(new ConfigFileReadEvent(
                Source: "config-loader",
                Path: path,
                Kind: ConfigFileKind.AgentSmithYml,
                SizeBytes: sizeBytes,
                RunId: null,
                Timestamp: DateTimeOffset.UtcNow));
        }
        catch
        {
            /* fire-and-warn — never break configuration load on a publish failure */
        }
    }
}
