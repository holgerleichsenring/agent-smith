namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0267: resolves the declared <c>output_schema</c> (observation / plan / diff / …)
/// of a master skill by name. The findings scrape on the api-security path is gated
/// on the master's own declared contract (observation) rather than on pipeline name,
/// so a coding master (code + verdict) is never scraped as findings.
/// </summary>
public interface IMasterOutputSchemaResolver
{
    /// <summary>
    /// The master skill's declared output_schema, or <c>null</c> when the master is
    /// unknown / the catalog is not yet bootstrapped / no schema is declared.
    /// </summary>
    string? Resolve(string masterSkillName);
}
