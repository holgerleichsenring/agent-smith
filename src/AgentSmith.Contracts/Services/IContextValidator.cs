using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Validates generated .context.yaml against the CCS schema structure.
/// </summary>
public interface IContextValidator
{
    ContextValidationResult Validate(string yaml);
}
