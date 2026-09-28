# Metrics

Agent Smith exposes counters via `System.Diagnostics.Metrics` under the Meter name `AgentSmith`. The metrics surface is BCL-only — no NuGet dependency is added in the agent-smith binary. Operators who want Prometheus, OTLP, or any other export wire their own exporter against the named Meter.

This page lists the counters, explains the cost-of-ambiguity dashboard, and shows how to wire OpenTelemetry without modifying agent-smith.

## Overview

The meter is declared once, in `AgentSmith.Application/Services/Metrics/AgentSmithMetrics.cs`, a class registered as a singleton and injected where a counter is incremented:

```csharp
public sealed class AgentSmithMetrics : IDisposable
{
    public const string MeterName = "AgentSmith";
    public Counter<long> AmbiguousResolution { get; }
    public Counter<long> PipelineSkippedAsIrrelevant { get; }
}
```

Discovery is by name. Anything in your process — a hosted exporter, a `MeterListener` in a test, the `dotnet-counters` CLI — finds the counters by asking for instruments on the meter named `AgentSmith`.

Why BCL and not OpenTelemetry directly: the `OpenTelemetry.*` NuGet ecosystem churns. Pinning an exporter package inside agent-smith would force every operator onto that exporter and that version. `System.Diagnostics.Metrics` is the .NET-native abstraction; OpenTelemetry interops with it via `AddMeter("AgentSmith")`. Picking the BCL surface keeps the producer (agent-smith) and the exporter (operator) decoupled.

## The two counters

### `agent_smith_ambiguous_resolution_total`

| Property | Value |
|----------|-------|
| Type | `Counter<long>` |
| Labels | `project`, `pipeline` |
| Source | `ProjectResolver` |

Incremented **once per matched (project, pipeline) pair** when the resolver returns more than one match for an incoming ticket envelope. Single-match resolutions (the common case) emit nothing.

Example: a tag matches three projects A, B, C — each with one pipeline `code`. One ticket event produces three increments:

```
agent_smith_ambiguous_resolution_total{project="A",pipeline="code"} += 1
agent_smith_ambiguous_resolution_total{project="B",pipeline="code"} += 1
agent_smith_ambiguous_resolution_total{project="C",pipeline="code"} += 1
```

The per-pair increment is intentional. The dashboard question is "how often is project X picking up a ticket that other projects are also picking up?" — a per-project question. Operators who want "total ambiguous events" take `max()` across the per-project counts.

### `agent_smith_pipeline_skipped_as_irrelevant_total`

| Property | Value |
|----------|-------|
| Type | `Counter<long>` |
| Labels | `project`, `pipeline`, `reason` |
| Source | `EmptyPlanCheckHandler` |

Incremented when a pipeline's plan has no steps (`plan.Steps.Count == 0`) and the post-plan gate ("Check plan is non-empty") signals a graceful skip. The `reason` label has one value: `"empty_plan"`.

No built-in pipeline preset produces an empty plan, so on a stock installation this counter stays at zero. It moves only for operator-authored presets that include the empty-plan check.

Example: a multi-repo project fans out to three repos; one repo's Plan comes back empty. One increment:

```
agent_smith_pipeline_skipped_as_irrelevant_total{project="acme-product",pipeline="code",reason="empty_plan"} += 1
```

The skip is graceful — the run exits successfully, no PR is opened, no further handlers (Apply, Verify, Commit) execute. From the queue's point of view the job completed normally; from the operator's point of view there is no PR noise on a repo where the LLM found nothing to do.

## Cost-of-ambiguity dashboard

The ratio of the two counters per (project, pipeline) quantifies how often ambiguous fan-out leads to a no-op run:

```promql
rate(agent_smith_pipeline_skipped_as_irrelevant_total{reason="empty_plan"}[1h])
/
rate(agent_smith_ambiguous_resolution_total[1h])
```

Interpretation:

- **Ratio near 0** — most ambiguous fan-outs do produce real work. The tag is broad on purpose; the LLM is finding genuine cross-project relevance. Leave the resolution config alone.
- **Ratio near 1** — most ambiguous fan-outs end in empty plans. The tag is too broad; the LLM is being asked to triage out-of-scope work. Tighten `project_resolution` (split tags, scope by area-path, etc.) — see [project-resolution.md](../configuration/project-resolution.md).
- **Ratio > 1** — should not happen if labels are stable; the skipped counter cannot increment without a prior resolution. A persistent reading > 1 suggests a config reload mid-window, a clock skew, or a pipeline that emits a skip from a non-ambiguous resolution path (file a bug).

The denominator is zero for projects whose resolutions are always single-match — that is the desired state. The numerator stays at zero too; the ratio is undefined and PromQL drops the series, which is correct.

Per-project / per-pipeline breakdown:

```promql
sum by (project, pipeline) (rate(agent_smith_pipeline_skipped_as_irrelevant_total{reason="empty_plan"}[1h]))
/
sum by (project, pipeline) (rate(agent_smith_ambiguous_resolution_total[1h]))
```

This is the queryshape to graph as a heatmap or table — one row per (project, pipeline), one column per time bucket.

## Operator-side OpenTelemetry wiring

Agent Smith does not ship OpenTelemetry packages. The operator adds them in their own composition root (the `Program.cs` that hosts the agent-smith server, or a deployment-layer adapter), then wires `AddMeter("AgentSmith")`:

```csharp
// In your composition root, after adding agent-smith's services:
services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("AgentSmith")
        .AddOtlpExporter(otlp =>
        {
            otlp.Endpoint = new Uri("http://otel-collector:4317");
            otlp.Protocol = OtlpExportProtocol.Grpc;
        }));
```

The Prometheus pull exporter is equivalent:

```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("AgentSmith")
        .AddPrometheusExporter());

// expose /metrics on the existing Kestrel:
app.MapPrometheusScrapingEndpoint();
```

Required NuGet packages (operator-installed, version-pinned to your stack):

- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol` (OTLP) **or** `OpenTelemetry.Exporter.Prometheus.AspNetCore` (Prometheus)

> **Pitfall**: `AddMeter("AgentSmith")` is case-sensitive and must match the Meter name exactly. A typo produces no error at startup — the exporter silently observes nothing. If your dashboard never shows data, double-check the Meter name first.

The same `AddMeter("AgentSmith")` wires both counters automatically; there is no per-instrument registration.

## `dotnet-counters` local inspection

For local debugging — no exporter required:

```bash
dotnet tool install --global dotnet-counters     # once
dotnet-counters monitor -p <pid> --counters AgentSmith
```

The `--counters AgentSmith` flag subscribes to every instrument on the `AgentSmith` meter. Output refreshes every second, showing current values for both counters and any labels in play.

To capture a counter trace to a file (useful when reproducing a tricky ambiguity case):

```bash
dotnet-counters collect -p <pid> --counters AgentSmith --output agent-smith-metrics.csv
```

## See also

- [Repos: multi-repo](../../connect-your-stuff/repos-multi.md) — the design context the counters quantify.
- [Project Resolution Strategies](../configuration/project-resolution.md) — how ambiguity arises and how to tighten it.
- [Server resilience](server-resilience.md) — `/health` and the startup findings, the other operator-facing observability surface.
