using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Compiles <see cref="PatternDefinition"/> regexes into <see cref="CompiledPattern"/>
/// instances ready for matching. Skips entries with empty or invalid regexes.
/// </summary>
public sealed class PatternCompiler(ILogger<PatternCompiler> logger)
{
    public List<CompiledPattern> Compile(IReadOnlyList<PatternDefinition> definitions)
    {
        var compiled = new List<CompiledPattern>();

        foreach (var def in definitions)
        {
            if (string.IsNullOrWhiteSpace(def.Regex))
                continue;

            try
            {
                var regex = new Regex(
                    def.Regex,
                    RegexOptions.Compiled | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(500));

                compiled.Add(new CompiledPattern(regex, def));
            }
            catch (RegexParseException ex)
            {
                logger.LogWarning(ex, "Invalid regex in pattern {PatternId}: {Regex}", def.Id, def.Regex);
            }
        }

        return compiled;
    }
}
