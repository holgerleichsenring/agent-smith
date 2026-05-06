namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// Builds an ISandboxFileReader bound to a specific ISandbox. Singleton in DI;
/// the bound reader is created on demand inside a handler once the pipeline
/// sandbox has been resolved from PipelineContext.
/// </summary>
public interface ISandboxFileReaderFactory
{
    ISandboxFileReader Create(ISandbox sandbox);
}
