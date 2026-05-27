namespace AgentSmith.Application.Services.Validation;

/// <summary>
/// Thrown when a hand-written schema file fails to parse or fails its meta-schema check
/// at boot. Carries the schema resource path so the operator can locate the bad file.
/// </summary>
public sealed class JsonSchemaLoadException : Exception
{
    public string ResourcePath { get; }

    public JsonSchemaLoadException(string resourcePath, string message, Exception? inner = null)
        : base($"{resourcePath}: {message}", inner)
    {
        ResourcePath = resourcePath;
    }
}
