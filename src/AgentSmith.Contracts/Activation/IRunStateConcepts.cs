namespace AgentSmith.Contracts.Activation;

/// <summary>
/// Typed read/write surface for concept values during a pipeline run. Reads return the
/// vocabulary-declared default for unset concepts; writes throw at the call site on
/// type mismatch, out-of-range, unknown enum value, or undeclared concept name. Backed
/// by <c>PipelineContextRunStateConcepts</c> in production; tests substitute fakes.
/// </summary>
public interface IRunStateConcepts
{
    /// <summary>Reads a Bool concept. Returns false when unset. Throws on type mismatch or undeclared name.</summary>
    bool GetBool(string name);

    /// <summary>Reads an Int concept. Returns 0 when unset. Throws on type mismatch or undeclared name.</summary>
    int GetInt(string name);

    /// <summary>Reads an Enum concept. Returns the first declared enum value when unset. Throws on type mismatch or undeclared name.</summary>
    string GetEnum(string name);

    /// <summary>Stores a Bool value. Throws <see cref="ConceptTypeMismatchException"/> if the concept is not declared as Bool, or <see cref="KeyNotFoundException"/> if undeclared.</summary>
    void SetBool(string name, bool value);

    /// <summary>Stores an Int value. Throws <see cref="ConceptTypeMismatchException"/> on type mismatch, <see cref="ArgumentOutOfRangeException"/> outside the declared int_range, or <see cref="KeyNotFoundException"/> if undeclared.</summary>
    void SetInt(string name, int value);

    /// <summary>Stores an Enum value. Throws <see cref="ConceptTypeMismatchException"/> on type mismatch, <see cref="ArgumentException"/> when the value is not in the declared enum_values, or <see cref="KeyNotFoundException"/> if undeclared.</summary>
    void SetEnum(string name, string value);
}
