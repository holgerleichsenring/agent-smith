namespace AgentSmith.Application.Services.Pocs.Pipeline;

/// <summary>
/// 2026-11-23-933dd: the build pipeline as a GitHub Actions workflow, '.github/workflows/poc-build.yml' — dispatch only,
/// contents read, the inputs as workflow env; build checks out the folder at the sha without persisted credentials and
/// uploads the gzip tar for one day; scan runs the pinned Trivy container on it; push alone names the environment whose
/// secrets sign in to the registry.
/// </summary>
public sealed class GitHubPocWorkflow(PocPipelineResources resources, PocPipelineScripts scripts, PocYamlFill fill)
{
    public const string Path = ".github/workflows/poc-build.yml";

    public PocPipelineFile Render(PocPipelineValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var shape = new PocHostShape(
            Context: "$POC_FOLDER", ImageIn: "in/image.tar.gz", ScanOut: "report", PushOut: "out",
            TrivyRun: PocTrivyRun.Container(values.TrivyImage),
            Login: $"printf '%s' \"$POC_REGISTRY_PASSWORD\" | docker login \"{values.RegistryHost}\" -u \"$POC_REGISTRY_USER\" --password-stdin",
            RepoUrl: string.Empty, AuthOn: string.Empty, AuthOff: string.Empty);
        var scalars = PocTemplateScalars.Of(values);
        return new PocPipelineFile(Path, fill.Fill(resources.Text("github.yml"), scalars, scripts.For(values, shape)));
    }
}
