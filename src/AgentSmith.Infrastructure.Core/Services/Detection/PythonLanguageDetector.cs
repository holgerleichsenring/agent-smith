using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Detection;

public sealed class PythonLanguageDetector : ILanguageDetector
{
    public async Task<LanguageDetectionResult?> DetectAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        var pyprojectPath = Path.Combine(repoPath, "pyproject.toml");
        var setupPyPath = Path.Combine(repoPath, "setup.py");
        var requirementsPath = Path.Combine(repoPath, "requirements.txt");
        var pipfilePath = Path.Combine(repoPath, "Pipfile");

        var hasPyproject = await reader.ExistsAsync(pyprojectPath, cancellationToken);
        var hasSetupPy = await reader.ExistsAsync(setupPyPath, cancellationToken);
        var hasRequirements = await reader.ExistsAsync(requirementsPath, cancellationToken);
        var hasPipfile = await reader.ExistsAsync(pipfilePath, cancellationToken);

        if (!hasPyproject && !hasSetupPy && !hasRequirements && !hasPipfile)
            return null;

        var keyFiles = new List<string>();
        string? testCmd = null;
        string? packageManager = null;

        if (hasPyproject)
        {
            keyFiles.Add("pyproject.toml");
            var content = await reader.TryReadAsync(pyprojectPath, cancellationToken) ?? string.Empty;

            if (content.Contains("[tool.pytest]") || content.Contains("[tool.pytest.ini_options]"))
                testCmd = "pytest";
            if (content.Contains("[tool.poetry]"))
                packageManager = "poetry";
            else if (content.Contains("hatchling") || content.Contains("[tool.hatch]"))
                packageManager = "hatch";
        }

        if (hasSetupPy) keyFiles.Add("setup.py");
        if (hasRequirements) keyFiles.Add("requirements.txt");
        if (hasPipfile) keyFiles.Add("Pipfile");

        packageManager ??= await DetectPackageManagerAsync(reader, repoPath, cancellationToken);
        testCmd ??= await DetectTestCommandAsync(reader, repoPath, cancellationToken);

        return new LanguageDetectionResult(
            Language: "Python",
            Runtime: "Python",
            PackageManager: packageManager,
            BuildCommand: null,
            TestCommand: testCmd,
            Frameworks: [],
            KeyFiles: keyFiles,
            Sdks: []);
    }

    private static async Task<string> DetectPackageManagerAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        if (await reader.ExistsAsync(Path.Combine(repoPath, "uv.lock"), cancellationToken)) return "uv";
        if (await reader.ExistsAsync(Path.Combine(repoPath, "Pipfile"), cancellationToken)) return "pipenv";
        return "pip";
    }

    private static async Task<string> DetectTestCommandAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        if (await reader.ExistsAsync(Path.Combine(repoPath, "tox.ini"), cancellationToken))
            return "tox";

        var makefile = await reader.TryReadAsync(Path.Combine(repoPath, "Makefile"), cancellationToken);
        if (makefile is not null && makefile.Contains("test:"))
            return "make test";

        return "pytest";
    }
}
