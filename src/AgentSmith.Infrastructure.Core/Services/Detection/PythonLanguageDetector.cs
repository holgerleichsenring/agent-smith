using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Detection;

public sealed class PythonLanguageDetector : ILanguageDetector
{
    public LanguageDetectionResult? Detect(string repoPath)
    {
        var pyprojectPath = Path.Combine(repoPath, "pyproject.toml");
        var setupPyPath = Path.Combine(repoPath, "setup.py");
        var requirementsPath = Path.Combine(repoPath, "requirements.txt");
        var pipfilePath = Path.Combine(repoPath, "Pipfile");

        if (!File.Exists(pyprojectPath) && !File.Exists(setupPyPath)
            && !File.Exists(requirementsPath) && !File.Exists(pipfilePath))
            return null;

        var keyFiles = new List<string>();
        string? testCmd = null;
        string? packageManager = null;

        if (File.Exists(pyprojectPath))
        {
            keyFiles.Add("pyproject.toml");
            var content = TryReadFile(pyprojectPath) ?? "";

            if (content.Contains("[tool.pytest]") || content.Contains("[tool.pytest.ini_options]"))
                testCmd = "pytest";
            if (content.Contains("[tool.poetry]"))
                packageManager = "poetry";
            else if (content.Contains("hatchling") || content.Contains("[tool.hatch]"))
                packageManager = "hatch";
        }

        if (File.Exists(setupPyPath)) keyFiles.Add("setup.py");
        if (File.Exists(requirementsPath)) keyFiles.Add("requirements.txt");
        if (File.Exists(pipfilePath)) keyFiles.Add("Pipfile");

        packageManager ??= DetectPackageManager(repoPath);
        testCmd ??= DetectTestCommand(repoPath);

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

    private static string? TryReadFile(string path)
    {
        try { return File.ReadAllText(path); }
        catch (IOException) { return null; }
    }

    private static string DetectPackageManager(string repoPath)
    {
        if (File.Exists(Path.Combine(repoPath, "uv.lock"))) return "uv";
        if (File.Exists(Path.Combine(repoPath, "Pipfile"))) return "pipenv";
        return "pip";
    }

    private static string DetectTestCommand(string repoPath)
    {
        if (File.Exists(Path.Combine(repoPath, "tox.ini")))
            return "tox";

        var makefile = Path.Combine(repoPath, "Makefile");
        if (File.Exists(makefile))
        {
            try
            {
                var content = File.ReadAllText(makefile);
                if (content.Contains("test:")) return "make test";
            }
            catch (IOException) { }
        }

        return "pytest";
    }
}
