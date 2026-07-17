"use client";

import { useCallback, useEffect, useState } from "react";
import type { ConfigChange, ConfigChangeAction } from "@/lib/configApi";
import { fetchChanges, revertChange } from "@/lib/configApi";
import { Badge, type BadgeTone } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { ENTITY_SINGULAR } from "./entities";

// p0345: the Changes view — the attributed, revertible audit trail that is THE
// argument for a DB-backed config over a hand-edited map. Each row carries who /
// when / what-diff and a Revert affordance; reverting posts to the change's
// revert endpoint and reloads the feed.

const ACTION_TONE: Record<ConfigChangeAction, BadgeTone> = {
  create: "green",
  update: "amber",
  delete: "rose",
  revert: "neutral",
};

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

  if (loading) return <p className="dsh-body text-stone-400">Loading changes…</p>;
  if (error)
    return (
      <p data-testid="config-changes-error" className="dsh-body text-rose-600">
        {error}
      </p>
    );

  return (
    <div className="flex flex-col gap-3" data-testid="config-changes">
      {changes.length === 0 && (
        <p data-testid="config-changes-empty" className="dsh-body text-stone-400">
          No changes recorded yet.
        </p>
      )}
      {changes.map((c) => (
        <div
          key={c.id}
          data-testid={`config-change-${c.id}`}
          className="card-content flex flex-col gap-2 p-4"
        >
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone={ACTION_TONE[c.action]} testId={`config-change-action-${c.id}`}>
              {c.action}
            </Badge>
            <span className="dsh-body text-stone-500">
              {ENTITY_SINGULAR[c.entityKind]}
            </span>
            <span className="dsh-mono font-mono font-semibold text-stone-900">{c.entityId}</span>
            <span className="ml-auto dsh-label text-stone-400" data-testid={`config-change-who-${c.id}`}>
              {c.actor} · {formatWhen(c.timestampUtc)}
            </span>
          </div>

          <div className="flex flex-col gap-1">
            {c.fields.map((f) => (
              <div
                key={f.field}
                data-testid={`config-change-diff-${c.id}-${f.field}`}
                className="flex flex-wrap items-center gap-2 dsh-mono font-mono"
              >
                <span className="text-stone-400">{f.field}</span>
                <span className="text-rose-600 line-through">{f.before ?? "∅"}</span>
                <span className="text-stone-400">→</span>
                <span className="text-emerald-700">{f.after ?? "∅"}</span>
              </div>
            ))}
          </div>

          <div className="flex items-center">
            {c.reverted ? (
              <span data-testid={`config-change-reverted-${c.id}`} className="dsh-label text-stone-400">
                reverted
              </span>
            ) : (
              <Button
                variant="ghost"
                onClick={() => revert(c.id)}
                disabled={reverting === c.id}
                data-testid={`config-change-revert-${c.id}`}
              >
                {reverting === c.id ? "Reverting…" : "Revert"}
              </Button>
            )}
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
