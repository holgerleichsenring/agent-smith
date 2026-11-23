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
import { refusalIn } from "@/lib/apiResponse";
import { RefusalSurface } from "@/components/shell/RefusalSurface";
import { ModelQuotaPanel } from "./ModelQuotaPanel";
import { useDateLocale, useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";

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
const CATEGORY_GROUPS: Array<{ key: string; label: TextKey; note?: TextKey }> = [
  { key: "service", label: "system.connections.service" },
  { key: "agent", label: "system.connections.agent", note: "system.connections.agentNote" },
  { key: "infra", label: "system.connections.infra" },
  { key: "chat", label: "system.connections.chat" },
];

export function ConnectionsView() {
  const t = useT();
  const [data, setData] = useState<ConnectionDiagnostics | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const [results, setResults] = useState<Record<string, ConnectionStatus>>({});
  const [probing, setProbing] = useState<Record<string, boolean>>({});

  useEffect(() => {
    const controller = new AbortController();
    fetchConnections(controller.signal)
      .then(setData)
      .catch((e: Error) => {
        if (e.name !== "AbortError") setError(e);
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

  const refusal = refusalIn(error);

  return (
    <div data-testid="connections-view">
      <PageHead
        title={t("system.connections.title")}
        sub={t("system.connections.sub")}
        right={
          data && data.connections.length > 0 ? (
            <button
              type="button"
              className="btn primary"
              onClick={probeAll}
              data-testid="connections-test-all"
            >
              {t("system.connections.testAll")}
            </button>
          ) : undefined
        }
      />

      {refusal ? (
        <RefusalSurface refusal={refusal} surface={t("system.connections.surface")} />
      ) : error ? (
        <div className="stateline err" data-testid="connections-error">
          {t("system.connections.failed", { message: error.message })}
        </div>
      ) : !data ? (
        <div className="stateline" data-testid="connections-loading">
          {t("system.connections.loading")}
        </div>
      ) : (
        <>
          {data.connections.length === 0 && (
            <div className="empty" data-testid="connections-empty">
              <div className="ei" aria-hidden>
                ◳
              </div>
              {t("system.connections.empty")}
            </div>
          )}
          {CATEGORY_GROUPS.map((group) => (
            <ConnectionGroup
              key={group.key}
              id={group.key}
              label={t(group.label)}
              note={group.note && t(group.note)}
              connections={data.connections.filter((c) => c.category === group.key)}
              results={results}
              probing={probing}
              onTest={probe}
            />
          ))}
          <ModelQuotaPanel />
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
  id,
  label,
  note,
  connections,
  results,
  probing,
  onTest,
}: {
  id: string;
  label: string;
  note?: string;
  connections: ConnectionDescriptor[];
  results: Record<string, ConnectionStatus>;
  probing: Record<string, boolean>;
  onTest: (name: string) => void;
}) {
  if (connections.length === 0) return null;
  return (
    <section data-testid={`connection-group-${id}`}>
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
  const t = useT();
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
            <span className="edit-hint mono num">{t("system.connections.latency", { ms: result.latencyMs })}</span>
          )}
          <button
            type="button"
            className="btn"
            onClick={onTest}
            disabled={probing}
            data-testid={`connection-test-${connection.name}`}
          >
            {probing ? t("system.connections.testing") : t("system.connections.test")}
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
  const t = useT();
  const locale = useDateLocale();
  return (
    <section data-testid="webhook-panel">
      <div className="section-head">
        <h2>{t("system.connections.webhooks")}</h2>
        <span className="cnt">{webhooks.length}</span>
        <span className="sh-sub">{t("system.connections.webhooksSub")}</span>
      </div>
      <div className="msub" style={{ marginTop: 10 }}>
        {t("system.connections.webhooksNote")}
      </div>
      <div style={{ height: 12 }} />
      <div className="rows">
        {webhooks.map((w) => (
          <div key={w.platform} className="lrow" data-testid={`webhook-row-${w.platform}`}>
            <span className="id">{w.platform}</span>
            <span>
              <span className={cn("tybadge", w.secretConfigured && "ok")}>
                {w.secretConfigured ? t("system.connections.secretConfigured") : t("system.connections.noSecret")}
              </span>
            </span>
            <span className="meta">
              {w.lastReceivedUtc
                ? t("system.connections.lastSeen", { at: new Date(w.lastReceivedUtc).toLocaleString(locale) })
                : t("system.connections.neverSeen")}
            </span>
            {/* p0506: the conjunction neither fact states on its own. A deployment that
                never received a delivery says nothing here. */}
            {w.acceptedUnsignedDelivery && (
              <span className="tybadge bad" data-testid={`webhook-unsigned-${w.platform}`}>
                {t("system.connections.unsigned")}
              </span>
            )}
          </div>
        ))}
      </div>
    </section>
  );
}
