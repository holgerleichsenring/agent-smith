"use client";

import { useCallback, useEffect, useState } from "react";
import type { ConfigChange } from "@/lib/configApi";
import { fetchChanges, revertChange } from "@/lib/configApi";
import { ENTITY_SINGULAR } from "./entities";

// p0345: the Changes view — the attributed, revertible audit trail that is THE
// argument for a DB-backed config over a hand-edited map. Each row carries who /
// when / what-diff and a Revert affordance.
// p0343c (pixel identity): audit rows render as the mock's .ecard entries —
// ✚/✎ icon block, the diff as the (sans) card name, "target · by who · when" as
// the sub line, the change id as the type badge and "revert ↺" as the edit hint.

export function ChangesView({ onReverted }: { onReverted?: () => void }) {
  const [changes, setChanges] = useState<ConfigChange[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reverting, setReverting] = useState<string | null>(null);

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setError(null);
    try {
      setChanges(await fetchChanges(signal));
    } catch (err) {
      if ((err as Error).name === "AbortError") return;
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  async function revert(id: string) {
    setReverting(id);
    try {
      await revertChange(id);
      await load();
      onReverted?.();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setReverting(null);
    }
  }

  if (loading) return <div className="empty">Loading changes…</div>;
  if (error)
    return (
      <div data-testid="config-changes-error" className="empty" style={{ color: "var(--bad)" }}>
        {error}
      </div>
    );

  return (
    <div className="list" data-testid="config-changes">
      {changes.length === 0 && (
        <div data-testid="config-changes-empty" className="empty">
          <div className="ei">◔</div>
          No changes recorded yet.
        </div>
      )}
      {changes.map((c) => (
        <div key={c.id} data-testid={`config-change-${c.id}`} className="ecard" style={{ cursor: "default" }}>
          <div className="ec-top">
            <div className="ec-ic">{c.action === "create" ? "✚" : c.action === "delete" ? "✕" : "✎"}</div>
            <div>
              <div className="ec-name sans">
                {c.fields.length > 0 ? (
                  c.fields.map((f) => (
                    <span
                      key={f.field}
                      data-testid={`config-change-diff-${c.id}-${f.field}`}
                      style={{ display: "block" }}
                    >
                      {f.field}: <span style={{ color: "var(--bad)" }}>{f.before ?? "∅"}</span>
                      {" → "}
                      <span style={{ color: "var(--ok)" }}>{f.after ?? "∅"}</span>
                    </span>
                  ))
                ) : (
                  <span>{c.action}</span>
                )}
              </div>
              <div className="ec-sub">
                {ENTITY_SINGULAR[c.entityKind]} / {c.entityId} · by{" "}
                <b data-testid={`config-change-who-${c.id}`}>{c.actor}</b> · {formatWhen(c.timestampUtc)}
              </div>
            </div>
            <div className="ec-right">
              <span className="tybadge" data-testid={`config-change-action-${c.id}`}>
                {c.action}
              </span>
              {c.reverted ? (
                <span className="edit-hint" data-testid={`config-change-reverted-${c.id}`}>
                  reverted
                </span>
              ) : (
                <button
                  type="button"
                  className="edit-hint"
                  onClick={() => revert(c.id)}
                  disabled={reverting === c.id}
                  data-testid={`config-change-revert-${c.id}`}
                >
                  {reverting === c.id ? "reverting…" : "revert ↺"}
                </button>
              )}
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

function formatWhen(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString();
}
