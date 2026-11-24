using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Pocs;
using AgentSmith.Infrastructure.Services.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Pocs.Scan;

/// <summary>2026-11-23-933dc: a skills catalog root holding only the pattern files a PoC scan test writes — copies of
/// catalog patterns (aws-access-key, google-api-key, generic-secret-assignment) plus whatever the case adds.</summary>
internal sealed class PocPatternsDirectory : ISkillsCatalogPath, IDisposable
{
    internal const string Secrets = """
        name: secrets
        patterns:
          - id: aws-access-key
            regex: "AKIA[0-9A-Z]{16}"
            severity: critical
            confidence: 9
            title: "AWS Access Key ID"
            description: "Hardcoded AWS access key identifier"
          - id: google-api-key
            regex: "AIza[0-9A-Za-z\\-_]{35}"
            severity: high
            confidence: 8
            title: "Google API Key"
            description: "Hardcoded Google API key"
          - id: generic-secret-assignment
            regex: "(?i)(secret|password|passwd|pwd|token)\\s*[=:]\\s*[\"'][^\"']{8,}[\"']"
            severity: medium
            confidence: 4
            title: "Generic Secret Assignment"
            description: "A secret-looking value assigned in code"
        """;

    public PocPatternsDirectory(string secrets = Secrets, params (string Name, string Yaml)[] others)
    {
        Directory.CreateDirectory(System.IO.Path.Combine(Root, "patterns"));
        File.WriteAllText(System.IO.Path.Combine(Root, "patterns", "secrets.yaml"), secrets);
        foreach (var (name, yaml) in others) File.WriteAllText(System.IO.Path.Combine(Root, "patterns", name + ".yaml"), yaml);
    }

    public string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "as-933dc-patterns-" + Guid.NewGuid().ToString("N"));

    public string Origin => "test";

    internal PocSecretScan Scan() => new(
        new PocPatternSet(new PatternsDirectoryResolver(this), new PatternDefinitionLoader(NullLogger<PatternDefinitionLoader>.Instance),
            new PatternCompiler(NullLogger<PatternCompiler>.Instance)),
        new PatternLineMatcher(), new PocLineMasker(), new PocTextFiles());

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (IOException) { }
    }
}
