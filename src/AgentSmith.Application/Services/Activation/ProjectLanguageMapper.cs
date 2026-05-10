namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// Maps the free-form <c>ProjectMap.PrimaryLanguage</c> string into the closed
/// <c>project_language</c> enum (csharp / node / python / generic). The mapping
/// table is fixed code per p0130c — adding a 5th language is a deliberate
/// vocabulary change, not a config-knob.
/// </summary>
public static class ProjectLanguageMapper
{
    private const string Csharp = "csharp";
    private const string Node = "node";
    private const string Python = "python";
    private const string Generic = "generic";

    private static readonly Dictionary<string, string> Synonyms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["csharp"] = Csharp,
            ["c#"] = Csharp,
            [".net"] = Csharp,
            ["dotnet"] = Csharp,
            ["typescript"] = Node,
            ["javascript"] = Node,
            ["node"] = Node,
            ["node.js"] = Node,
            ["nodejs"] = Node,
            ["ts"] = Node,
            ["js"] = Node,
            ["python"] = Python,
            ["py"] = Python,
        };

    public static string Map(string? primaryLanguage)
    {
        if (string.IsNullOrWhiteSpace(primaryLanguage)) return Generic;
        var trimmed = primaryLanguage.Trim();
        return Synonyms.TryGetValue(trimmed, out var mapped) ? mapped : Generic;
    }
}
