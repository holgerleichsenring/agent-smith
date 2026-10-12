using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-06-8565b: measures one source file's syntactic units — types, functions and the
/// file itself — so a principles file's Limits can be judged against them. Which languages it
/// knows is data (one grammar and query per language), never a branch in code.
/// </summary>
public interface IStructureEngine
{
    /// <summary>
    /// Measures <paramref name="content"/>, read from the repository-relative
    /// <paramref name="path"/>. A path no language claims, or one excluded as test or generated
    /// code, is NotMeasured; a grammar that cannot be loaded is Unavailable, never a silent pass.
    /// </summary>
    StructureMeasurement Measure(string path, string content);
}
