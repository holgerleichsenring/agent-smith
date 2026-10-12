namespace AgentSmith.Contracts.Models.Structure;

/// <summary>
/// 2026-10-06-8565b: the closed Limits keys of the catalog's DELTA-FORMAT. A key outside this
/// set fails the catalog's validator; here it is ignored.
/// </summary>
public static class StructureMetric
{
    public const string FunctionLines = "function_lines";
    public const string TypeLines = "type_lines";
    public const string TypesPerFile = "types_per_file";
    public const string FileLines = "file_lines";

    /// <summary>The key a file-level finding carries.</summary>
    public const string FileKey = "<file>";

    public static IReadOnlyList<string> All { get; } =
        [FunctionLines, TypeLines, TypesPerFile, FileLines];
}
