namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-10-01-283dd: an image a tool hands the loop — a browser render, a design export.
/// The producer encodes and bounds it (png, jpeg, gif or webp; at most 5 MB; long edge at
/// most 1568 px); the caption is one line saying what the picture shows.
/// </summary>
public sealed record ToolImage(string MediaType, byte[] Bytes, string Caption);
