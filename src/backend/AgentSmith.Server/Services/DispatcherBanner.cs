namespace AgentSmith.Server.Services;

/// <summary>
/// Prints the Agent Smith Dispatcher ASCII banner to the console on startup,
/// followed by the binary's release version (read from /app/version.txt,
/// release-please's source of truth, copied into the image by the Dockerfile).
/// Operators read the version off the startup log to confirm a pulled image
/// actually carries the expected release.
/// </summary>
internal static class DispatcherBanner
{
    public static void Print()
    {
        var original = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(@"
  █████╗  ██████╗ ███████╗███╗   ██╗████████╗    ███████╗███╗   ███╗██╗████████╗██╗  ██╗
 ██╔══██╗██╔════╝ ██╔════╝████╗  ██║╚══██╔══╝    ██╔════╝████╗ ████║██║╚══██╔══╝██║  ██║
 ███████║██║  ███╗█████╗  ██╔██╗ ██║   ██║       ███████╗██╔████╔██║██║   ██║   ███████║
 ██╔══██║██║   ██║██╔══╝  ██║╚██╗██║   ██║       ╚════██║██║╚██╔╝██║██║   ██║   ██╔══██║
 ██║  ██║╚██████╔╝███████╗██║ ╚████║   ██║       ███████║██║ ╚═╝ ██║██║   ██║   ██║  ██║
 ╚═╝  ╚═╝ ╚═════╝ ╚══════╝╚═╝  ╚═══╝  ╚═╝       ╚══════╝╚═╝     ╚═╝╚═╝   ╚═╝   ╚═╝  ╚═╝");
        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine($"  Dispatcher · v{ReadVersion()} · Slack / Teams · Redis Streams · Docker / K8s Jobs\n");
        Console.ForegroundColor = original;
    }

    private static string ReadVersion()
    {
        // Production: /app/version.txt (copied by Dockerfile).
        // Local dev: walk up from the binary dir until version.txt is found
        // (repo root). Unknown returns "<unknown>" so the banner never
        // throws.
        try
        {
            const string fileName = "version.txt";
            if (File.Exists(Path.Combine("/app", fileName)))
                return File.ReadAllText(Path.Combine("/app", fileName)).Trim();
            var dir = AppContext.BaseDirectory;
            for (var i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
            {
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate).Trim();
                dir = Path.GetDirectoryName(dir)!;
            }
        }
        catch { /* fall through to unknown */ }
        return "<unknown>";
    }
}
