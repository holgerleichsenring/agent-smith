"use client";

import { useState } from "react";
import type { CatalogEntry } from "@/lib/catalogApi";
import { Markdown } from "@/components/ui/Markdown";
import { CatalogSourceNote } from "./CatalogSourceNote";

// p0221 / p0343d: one inventory card — the mock's .ecard, whose whole top row toggles and
// whose SKILL.md body is fetched lazily on expand.
// 2026-09-18-84be: the expanded body carries the source note, because the body is the one
// piece of catalog text on this page a reader might otherwise try to edit here.

export function CatalogEntryCard({
  entry,
  icon,
  loadBody,
  overlayPath,
}: {
  entry: CatalogEntry;
  icon: string;
  loadBody: (name: string) => Promise<string | null>;
  overlayPath: string | null;
}) {
  const [open, setOpen] = useState(false);
  const [body, setBody] = useState<string | null | undefined>(undefined);
  const [loading, setLoading] = useState(false);

  async function toggle() {
    const next = !open;
    setOpen(next);
    if (next && body === undefined && !loading) {
      setLoading(true);
      try {
        setBody(await loadBody(entry.name));
      } catch {
        setBody(null);
      } finally {
        setLoading(false);
      }
    }
  }

  return (
    <div className="ecard" data-testid={`catalog-entry-${entry.name}`}>
      <button
        type="button"
        className="ec-top"
        onClick={toggle}
        aria-expanded={open}
        data-testid={`catalog-entry-toggle-${entry.name}`}
      >
        <div className="ec-ic" aria-hidden>
          {icon}
        </div>
        <div style={{ minWidth: 0 }}>
          <div className="ec-name">{entry.name}</div>
          <div className="ec-sub">{entry.description}</div>
        </div>
        <div className="ec-right">
          <span className="tybadge">{entry.role}</span>
          <span className="ec-open">{open ? "close ▴" : "open ▾"}</span>
        </div>
      </button>
      {open && (
        <div className="ec-body" data-testid={`catalog-entry-body-${entry.name}`}>
          {loading ? (
            <span className="msub mono">loading…</span>
          ) : body ? (
            <Markdown>{body}</Markdown>
          ) : (
            <span className="msub mono">No SKILL.md body.</span>
          )}
          <CatalogSourceNote
            overlayPath={overlayPath}
            testId={`catalog-entry-source-${entry.name}`}
          />
        </div>
      )}
    </div>
  );
}
