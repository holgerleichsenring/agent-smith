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
import { PageHead } from "./PageHead";
import { cn } from "@/lib/utils";

// p0292/p0293: the System → Connections view. The static ConfigView answers "how
// is it wired"; this answers "does it actually work right now". Every runtime
// connection — repos, trackers, agents (LLM), and infra (Redis / persistence /
// sandbox) plus any configured chat adapter — is listed once, grouped by category,
// with a Test button that runs a live, read-only probe. Webhooks are inbound and
// cannot be actively probed, so their panel shows only the honest facts: secret
// configured + last delivery seen. Container registry is intentionally absent — it
// is deploy infrastructure (k8s / docker-compose), not a runtime connection.
// p0343d: parity re-dress — .m-head with the Test-all primary action, category
// groups behind .section-head rules with .cnt counts, one .ecard per connection
// (status pill, name, type·kind sub-line, latency + Test on the right), webhook
// facts as .lrow rows. Data, probes and behaviors unchanged.

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
    <div data-testid="connections-view">
      <PageHead
        title="Connections"
        sub="Test whether every runtime connection — repositories, trackers, agents, and infrastructure — actually answers with its credentials. Probes are read-only and run only when you ask."
        right={
          data && data.connections.length > 0 ? (
            <button
              type="button"
              className="btn primary"
              onClick={probeAll}
              data-testid="connections-test-all"
            >
              Test all
            </button>
          ) : undefined
        }
      />

      {error ? (
        <div className="stateline err" data-testid="connections-error">
          Failed to load connections: {error}
        </div>
      ) : !data ? (
        <div className="stateline" data-testid="connections-loading">
          Loading connections…
        </div>
      ) : (
        <>
          {data.connections.length === 0 && (
            <div className="empty" data-testid="connections-empty">
              <div className="ei" aria-hidden>
                ◳
              </div>
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
    <section data-testid={`connection-group-${label}`}>
      <div className="section-head">
        <h2>{label}</h2>
        <span className="cnt">{connections.length}</span>
        {note && <span className="sh-sub">{note}</span>}
      </div>
      <div style={{ height: 14 }} />
      <div className="list">
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
    </section>
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
    <div className="ecard" data-testid={`connection-row-${connection.name}`}>
      <div className="ec-top">
        <StatusPill status={statusFor(result)} />
        <div style={{ minWidth: 0 }}>
          <div className="ec-name sans">{connection.name}</div>
          <div className="ec-sub">
            {connection.type} · {connection.kind}
          </div>
        </div>
        <div className="ec-right">
          {result?.ok && (
            <span className="edit-hint mono num">{result.latencyMs} ms</span>
          )}
          <button
            type="button"
            className="btn"
            onClick={onTest}
            disabled={probing}
            data-testid={`connection-test-${connection.name}`}
          >
            {probing ? "Testing…" : "Test"}
          </button>
        </div>
      </div>
      {result && !result.ok && result.error && (
        <div className="ec-body">
          <p className="errline" data-testid={`connection-error-${connection.name}`}>
            {result.error}
          </p>
        </div>
      )}
    </div>
  );
}

function WebhookPanel({ webhooks }: { webhooks: WebhookStatus[] }) {
  return (
    <section data-testid="webhook-panel">
      <div className="section-head">
        <h2>Webhooks</h2>
        <span className="cnt">{webhooks.length}</span>
        <span className="sh-sub">inbound — cannot be actively tested from here</span>
      </div>
      <div className="msub" style={{ marginTop: 10 }}>
        A webhook is inbound, so it cannot be actively tested from here — trigger a
        real event on the platform to see a delivery. These are the facts we can
        show: whether a signing secret is configured, and when the last delivery
        arrived.
      </div>
      <div style={{ height: 12 }} />
      <div className="rows">
        {webhooks.map((w) => (
          <div key={w.platform} className="lrow" data-testid={`webhook-row-${w.platform}`}>
            <span className="id">{w.platform}</span>
            <span>
              <span className={cn("tybadge", w.secretConfigured && "ok")}>
                {w.secretConfigured ? "secret configured" : "no secret"}
              </span>
            </span>
            <span className="meta">
              {w.lastReceivedUtc
                ? `last seen ${new Date(w.lastReceivedUtc).toLocaleString()}`
                : "never seen"}
            </span>
          </div>
        ))}
      </div>
    </section>
  );
}
