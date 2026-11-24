namespace AgentSmith.Application.Services.Pocs.Pipeline;

/// <summary>
/// 2026-11-23-933dd: the build pipeline as Azure DevOps YAML, '&lt;template_path&gt;/azure-pipelines.poc-build.yml' — no
/// trigger, the inputs as template parameters mapped into each step's env; build checks out nothing and fetches the sha
/// of the folder alone with System.AccessToken in that step only; push alone uses the Docker registry service
/// connection. The operator creates the pipeline from this file once.
/// </summary>
public sealed class AzurePocPipeline(PocPipelineResources resources, PocPipelineScripts scripts, PocYamlFill fill)
{
    public const string FileName = "azure-pipelines.poc-build.yml";

    public PocPipelineFile Render(PocPipelineValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var shape = new PocHostShape(
            Context: "src/$POC_FOLDER", ImageIn: "poc-image-tar/image.tar.gz", ScanOut: "report", PushOut: "out",
            TrivyRun: PocTrivyRun.Container(values.TrivyImage), Login: string.Empty, RepoUrl: string.Empty,
            AuthOn: "git config http.extraheader \"AUTHORIZATION: bearer $SYSTEM_ACCESSTOKEN\"",
            AuthOff: "git config --unset-all http.extraheader");
        var blocks = new Dictionary<string, string>(scripts.For(values, shape)) { ["ENV"] = resources.Text("azure.env.yml") };
        var path = $"{values.TemplatePath.TrimEnd('/')}/{FileName}";
        return new PocPipelineFile(path, fill.Fill(resources.Text("azure.yml"), PocTemplateScalars.Of(values), blocks));
    }
}
