using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Cli;

/// <summary>
/// Prints a dry-run summary for any pipeline type.
/// </summary>
internal static class DryRunPrinter
{
    public static void Print(PipelineRequest request, Dictionary<string, string>? extraInfo = null)
    {
        var commands = PipelinePresets.TryResolve(request.PipelineName);
        if (commands is null)
        {
            Console.Error.WriteLine($"Pipeline '{request.PipelineName}' not found in presets.");
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine("Dry run - would execute:");
        Console.WriteLine($"  Project:  {request.ProjectName}");
        Console.WriteLine($"  Pipeline: {request.PipelineName}");

        if (request.TicketId is not null)
            Console.WriteLine($"  Ticket:   #{request.TicketId}");

        if (extraInfo is not null)
        {
            foreach (var (key, value) in extraInfo)
                Console.WriteLine($"  {key}: {value}");
        }

        Console.WriteLine("  Commands:");
        foreach (var cmd in commands)
            Console.WriteLine($"    - {cmd}");
    }
}
