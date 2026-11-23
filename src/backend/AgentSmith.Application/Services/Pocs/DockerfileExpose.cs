namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: the one TCP port a PoC serves on — exactly one across the EXPOSE instructions of the FINAL stage
/// (a builder stage's EXPOSE never runs, so it is ignored). A UDP-only EXPOSE, a range or a variable gives no port the
/// route could point at.
/// </summary>
public sealed class DockerfileExpose
{
    private const int MaxPort = 65535; // not a limit: the highest TCP port number

    /// <summary>The port, or the refusal.</summary>
    internal (int Port, PocRuleRefusal? Refusal) PortOf(DockerfileDocument document)
    {
        var ports = new List<(int Port, int Line)>();
        foreach (var expose in document.FinalStage.Where(i => i.Keyword == "EXPOSE"))
            foreach (var token in expose.Tokens)
            {
                var (number, protocol) = Split(token);
                if (protocol != "tcp") continue;
                if (!int.TryParse(number, out var port) || port is < 1 or > MaxPort)
                    return (0, Refused($"Dockerfile line {expose.Line}: EXPOSE {token} is not one port number"));
                ports.Add((port, expose.Line));
            }
        return ports.Count == 1
            ? (ports[0].Port, null)
            : (0, Refused($"the Dockerfile's final stage exposes {ports.Count} TCP ports"
                + (ports.Count == 0 ? "" : $" ({string.Join(", ", ports.Select(p => $"{p.Port} on line {p.Line}"))})")));
    }

    private static (string Number, string Protocol) Split(string token)
    {
        var slash = token.IndexOf('/');
        return slash < 0 ? (token, "tcp") : (token[..slash], token[(slash + 1)..].ToLowerInvariant());
    }

    private static PocRuleRefusal Refused(string what) =>
        new(PocArchiveChecks.Expose, $"{what}; it must expose exactly one TCP port, because that is the port the PoC's "
            + "address is routed to.");
}
