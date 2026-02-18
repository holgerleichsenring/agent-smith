# Phase 12: Phase-Aware Token Tracking - Implementation Details

## Overview
Extend TokenUsageTracker to track which tokens belong to which execution phase.
This enables accurate cost breakdown by phase (scout, planning, primary, compaction).

---

## TokenUsageTracker Extensions

### New Fields
```csharp
private readonly Dictionary<string, PhaseUsage> _phases = new();
private string _currentPhase = "primary";
```

### New Methods
- `SetPhase(string phase)` - Sets the current phase for subsequent Track() calls
- `GetPhaseBreakdown()` - Returns per-phase token usage

### Track() Changes
Each `Track()` call now also accumulates into the current phase:

```csharp
public void Track(MessageResponse response)
{
    // ... existing total tracking ...
    
    if (!_phases.TryGetValue(_currentPhase, out var phaseUsage))
    {
        phaseUsage = new PhaseUsage();
        _phases[_currentPhase] = phaseUsage;
    }
    phaseUsage.InputTokens += usage.InputTokens;
    phaseUsage.OutputTokens += usage.OutputTokens;
    phaseUsage.CacheReadTokens += usage.CacheReadInputTokens;
    phaseUsage.Iterations++;
}
```

---

## PhaseUsage Class

```csharp
public sealed class PhaseUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheReadTokens { get; set; }
    public int Iterations { get; set; }
}
```

---

## Integration in ClaudeAgentProvider

Phase transitions:
1. Scout: `tracker.SetPhase("scout")` before ScoutAgent runs
2. Primary: `tracker.SetPhase("primary")` before AgenticLoop runs
3. Compaction: ClaudeContextCompactor temporarily switches to "compaction" phase

The CostTracker maps each phase name to its model for pricing calculation.
