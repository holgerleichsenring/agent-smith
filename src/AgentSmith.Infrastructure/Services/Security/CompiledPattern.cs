using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// A pattern definition with its compiled regex, ready for matching.
/// </summary>
public sealed record CompiledPattern(Regex Regex, PatternDefinition Definition);
