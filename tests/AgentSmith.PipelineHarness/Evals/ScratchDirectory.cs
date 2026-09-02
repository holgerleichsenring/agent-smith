namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-09-01-6686: a temp directory that removes itself. A passive api scan still needs a
/// real local path to scope writes inside — production's passive shape carries one — and a
/// measurement that leaves a tree behind on every run is a measurement nobody runs twice.
/// </summary>
internal sealed class ScratchDirectory : IDisposable
{
    private ScratchDirectory(string path) => Path = path;

    internal string Path { get; }

    internal static ScratchDirectory Create(string prefix)
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():n}");
        Directory.CreateDirectory(path);
        return new ScratchDirectory(path);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp tree is not worth failing a measurement over.
        }
    }
}
