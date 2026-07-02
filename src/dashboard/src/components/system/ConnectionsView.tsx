"use client";

import { useCallback, useEffect, useState } from "react";
import {
  fetchConnections,
  probeConnection,
  type ConnectionDescriptor,
  type ConnectionDiagnostics,
  type ConnectionStatus,
  type WebhookStatus,
} from "@/lib/diagnosticsApi";
import { StatusPill } from "./StatusPill";
import type { ProviderStatus } from "@/hooks/useSystemStatus";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { SectionLabel } from "@/components/ui/SectionLabel";

// p0292/p0293: the System → Connections view. The static ConfigView answers "how
// is it wired"; this answers "does it actually work right now". Every runtime
// connection — repos, trackers, agents (LLM), and infra (Redis / persistence /
// sandbox) plus any configured chat adapter — is listed once, grouped by category,
// with a Test button that runs a live, read-only probe. Webhooks are inbound and
// cannot be actively probed, so their panel shows only the honest facts: secret
// configured + last delivery seen. Container registry is intentionally absent — it
// is deploy infrastructure (k8s / docker-compose), not a runtime connection.

// Display order + labels for the connection categories the backend emits. The
// agent group carries a cost note: unlike the read-only probes, an agent test
// spends a tiny LLM call.
const CATEGORY_GROUPS: Array<{ key: string; label: string; note?: string }> = [
  { key: "service", label: "Repositories & trackers" },
  { key: "agent", label: "Agents", note: "Each test spends a minimal (1-token) LLM call." },
  { key: "infra", label: "Infrastructure" },
  { key: "chat", label: "Chat" },
];

export function ConnectionsView() {
  const [data, setData] = useState<ConnectionDiagnostics | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [results, setResults] = useState<Record<string, ConnectionStatus>>({});
  const [probing, setProbing] = useState<Record<string, boolean>>({});

  useEffect(() => {
    const controller = new AbortController();
    fetchConnections(controller.signal)
      .then(setData)
      .catch((e: Error) => {
        if (e.name !== "AbortError") setError(e.message);
      });
    return () => controller.abort();
  }, []);

  const probe = useCallback(async (name: string) => {
    setProbing((p) => ({ ...p, [name]: true }));
    try {
      const status = await probeConnection(name);
      setResults((r) => ({ ...r, [name]: status }));
    } catch (e) {
      setResults((r) => ({
        ...r,
        [name]: { name, type: "", kind: "", category: "", ok: false, latencyMs: 0, error: (e as Error).message },
      }));
    } finally {
      setProbing((p) => ({ ...p, [name]: false }));
    }
  }, []);

  const probeAll = useCallback(async () => {
    if (!data) return;
    await Promise.all(data.connections.map((c) => probe(c.name)));
  }, [data, probe]);

  return (
    <div className="flex h-full flex-col overflow-y-auto" data-testid="connections-view">
      <div className="content-shell pb-0">
        <div className="flex items-start justify-between gap-4">
          <div>
            <SectionLabel>Connections</SectionLabel>
            <p className="mt-1 dsh-body text-stone-500">
              Test whether every runtime connection — repositories, trackers, agents,
              and infrastructure — actually answers with its credentials. Probes are
              read-only and run only when you ask.
            </p>
          </div>
          {data && data.connections.length > 0 && (
            <Button variant="primary" onClick={probeAll} data-testid="connections-test-all">
              Test all
            </Button>
          )}
        </div>
      </div>

      {error ? (
        <div className="content-shell dsh-body text-rose-700" data-testid="connections-error">
          Failed to load connections: {error}
        </div>
      ) : !data ? (
        <div className="content-shell dsh-body text-stone-400" data-testid="connections-loading">
          Loading connections…
        </div>
      ) : (
        <>
          {data.connections.length === 0 && (
            <div className="content-shell pt-4 dsh-body text-stone-500" data-testid="connections-empty">
              No connections configured.
            </div>
          )}
          {CATEGORY_GROUPS.map((group) => (
            <ConnectionGroup
              key={group.key}
              label={group.label}
              note={group.note}
              connections={data.connections.filter((c) => c.category === group.key)}
              results={results}
              probing={probing}
              onTest={probe}
            />
          ))}
          <WebhookPanel webhooks={data.webhooks} />
        </>
      )}
    </div>
  );
}

function statusFor(result: ConnectionStatus | null): ProviderStatus {
  if (result === null) return "unknown";
  return result.ok ? "ok" : "disconnected";
}

function ConnectionGroup({
  label,
  note,
  connections,
  results,
  probing,
  onTest,
}: {
  label: string;
  note?: string;
  connections: ConnectionDescriptor[];
  results: Record<string, ConnectionStatus>;
  probing: Record<string, boolean>;
  onTest: (name: string) => void;
}) {
  if (connections.length === 0) return null;
  return (
    <div className="content-shell pt-5" data-testid={`connection-group-${label}`}>
      <SectionLabel>{label}</SectionLabel>
      {note && <p className="mt-1 dsh-body text-stone-500">{note}</p>}
      <div className="mt-3 space-y-3">
        {connections.map((c) => (
          <ConnectionRow
            key={c.name}
            connection={c}
            result={results[c.name] ?? null}
            probing={probing[c.name] ?? false}
            onTest={() => onTest(c.name)}
          />
        ))}
      </div>
    </div>
  );
}

function ConnectionRow({
  connection,
  result,
  probing,
  onTest,
}: {
  connection: ConnectionDescriptor;
  result: ConnectionStatus | null;
  probing: boolean;
  onTest: () => void;
}) {
  return (
    <Card className="px-4 py-3" data-testid={`connection-row-${connection.name}`}>
      <div className="flex items-center gap-3">
        <StatusPill status={statusFor(result)} />
        <span className="dsh-body font-semibold text-stone-800">{connection.name}</span>
        <Badge tone="neutral">{connection.type}</Badge>
        <Badge tone="neutral">{connection.kind}</Badge>
        <span className="ml-auto flex items-center gap-3">
          {result?.ok && (
            <span className="dsh-mono text-stone-400">{result.latencyMs} ms</span>
          )}
          <Button onClick={onTest} disabled={probing} data-testid={`connection-test-${connection.name}`}>
            {probing ? "Testing…" : "Test"}
          </Button>
        </span>
      </div>
      {result && !result.ok && result.error && (
        <p
          className="mt-2 break-all rounded-md border border-rose-200 bg-rose-50 px-3 py-1.5 dsh-mono text-rose-700"
          data-testid={`connection-error-${connection.name}`}
        >
          {result.error}
        </p>
      )}
    </Card>
  );
}

function WebhookPanel({ webhooks }: { webhooks: WebhookStatus[] }) {
  return (
    <div className="content-shell pt-6" data-testid="webhook-panel">
      <SectionLabel>Webhooks</SectionLabel>
      <p className="mt-1 dsh-body text-stone-500">
        A webhook is inbound, so it cannot be actively tested from here — trigger a
        real event on the platform to see a delivery. These are the facts we can
        show: whether a signing secret is configured, and when the last delivery
        arrived.
      </p>
      <div className="mt-3 space-y-2">
        {webhooks.map((w) => (
          <div
            key={w.platform}
            className="flex items-center gap-3 rounded-md border border-stone-200 bg-white px-4 py-2.5"
            data-testid={`webhook-row-${w.platform}`}
          >
            <span className="dsh-body font-semibold text-stone-800">{w.platform}</span>
            <Badge tone={w.secretConfigured ? "green" : "neutral"}>
              {w.secretConfigured ? "secret configured" : "no secret"}
            </Badge>
            <span className="ml-auto dsh-mono text-stone-500">
              {w.lastReceivedUtc
                ? `last seen ${new Date(w.lastReceivedUtc).toLocaleString()}`
                : "never seen"}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}
