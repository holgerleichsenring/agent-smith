namespace AgentSmith.Dispatcher;

/// <summary>
/// Prints the Agent Smith Dispatcher ASCII banner to the console on startup.
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
        Console.WriteLine("  Dispatcher · Slack / Teams · Redis Streams · Docker / K8s Jobs\n");
        Console.ForegroundColor = original;
    }
}
