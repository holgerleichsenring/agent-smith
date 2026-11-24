namespace AgentSmith.Application.Services.Pocs.Pipeline;

/// <summary>2026-11-23-933dd: on a hosted agent Trivy is the pinned image run over the job's directory, as a shell
/// function the scan script calls by the binary's name.</summary>
public static class PocTrivyRun
{
    public static string Container(string image) =>
        $"trivy() {{ docker run --rm -v \"$PWD:/work\" -w /work {image} \"$@\"; }}";
}
