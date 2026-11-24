namespace AgentSmith.Application.Services.Pocs.Pipeline;

/// <summary>
/// 2026-11-23-933dd: the build pipeline as GitLab CI, '.gitlab-ci.yml' at the root — every job ruled to the 'api'
/// pipeline source with POC_FOLDER set, so no push runs anything; no git strategy, the sha of the folder fetched in the
/// build job; build and push through a docker:dind service (a privileged runner); scan in the pinned Trivy image; push
/// alone declares the environment the protected push variables are scoped to.
/// </summary>
public sealed class GitLabPocCi(PocPipelineResources resources, PocPipelineScripts scripts, PocYamlFill fill)
{
    public const string Path = ".gitlab-ci.yml";

    public PocPipelineFile Render(PocPipelineValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var build = values.Build;
        var shape = new PocHostShape(
            Context: "src/$POC_FOLDER", ImageIn: "out/image.tar.gz", ScanOut: ".", PushOut: ".", TrivyRun: string.Empty,
            Login: $"printf '%s' \"${build.PushPasswordSecret}\" | docker login \"{values.RegistryHost}\" -u \"${build.PushUserSecret}\" --password-stdin",
            RepoUrl: "POC_REPO_URL=\"$CI_REPOSITORY_URL\"", AuthOn: string.Empty, AuthOff: string.Empty);
        return new PocPipelineFile(Path, fill.Fill(resources.Text("gitlab.yml"), PocTemplateScalars.Of(values), scripts.For(values, shape)));
    }
}
