using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>The series a bound turn may read, and edit when its poster may start runs.</summary>
public sealed record BoundSeries(ISeriesRead Read, ISeriesEdit? Edit);
