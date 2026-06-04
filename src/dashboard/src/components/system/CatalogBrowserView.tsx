"use client";

import { useMemo, useState } from "react";
import type { CatalogConcept, CatalogContents, CatalogEntry } from "@/lib/catalogApi";
import { Card } from "@/components/ui/Card";
import { Badge } from "@/components/ui/Badge";
import { SectionLabel } from "@/components/ui/SectionLabel";
import { Markdown } from "@/components/ui/Markdown";

// p0221: the catalog browser's presentational layer. Masters/skills render as
// cards whose SKILL.md body is fetched lazily on expand; concepts render as
// filterable rows showing name + type + definition. Takes the contents + a
// body loader as props so it is unit-testable without the fetch layer.

interface CatalogBrowserViewProps {
  contents: CatalogContents;
  loadBody: (name: string) => Promise<string | null>;
}

export function CatalogBrowserView({ contents, loadBody }: CatalogBrowserViewProps) {
  return (
    <div className="content-shell space-y-6" data-testid="catalog-browser">
      <EntrySection label="Masters" entries={contents.masters} loadBody={loadBody} />
      <EntrySection label="Skills" entries={contents.skills} loadBody={loadBody} />
      <ConceptSection concepts={contents.concepts} />
    </div>
  );
}

function EntrySection({
  label,
  entries,
  loadBody,
}: {
  label: string;
  entries: CatalogEntry[];
  loadBody: (name: string) => Promise<string | null>;
}) {
  if (entries.length === 0) return null;
  return (
    <section className="space-y-2">
      <SectionLabel>{`${label} · ${entries.length}`}</SectionLabel>
      {entries.map((entry) => (
        <EntryCard key={entry.name} entry={entry} loadBody={loadBody} />
      ))}
    </section>
  );
}

function EntryCard({
  entry,
  loadBody,
}: {
  entry: CatalogEntry;
  loadBody: (name: string) => Promise<string | null>;
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
    <Card className="p-3" data-testid={`catalog-entry-${entry.name}`}>
      <button
        type="button"
        onClick={toggle}
        aria-expanded={open}
        data-testid={`catalog-entry-toggle-${entry.name}`}
        className="flex w-full items-center gap-2 text-left"
      >
        <span className="text-stone-400">{open ? "▾" : "▸"}</span>
        <span className="dsh-body font-semibold text-stone-900">{entry.name}</span>
        <Badge tone="neutral">{entry.role}</Badge>
        <span className="truncate dsh-mono text-stone-500">{entry.description}</span>
      </button>
      {open && (
        <div className="mt-2 border-t border-stone-100 pt-2" data-testid={`catalog-entry-body-${entry.name}`}>
          {loading ? (
            <span className="dsh-mono text-stone-400">loading…</span>
          ) : body ? (
            <Markdown>{body}</Markdown>
          ) : (
            <span className="dsh-mono text-stone-400">No SKILL.md body.</span>
          )}
        </div>
      )}
    </Card>
  );
}

function ConceptSection({ concepts }: { concepts: CatalogConcept[] }) {
  const [filter, setFilter] = useState("");
  const shown = useMemo(() => {
    const needle = filter.trim().toLowerCase();
    if (!needle) return concepts;
    return concepts.filter(
      (c) => c.name.toLowerCase().includes(needle) || c.description.toLowerCase().includes(needle),
    );
  }, [concepts, filter]);

  if (concepts.length === 0) return null;
  return (
    <section className="space-y-2" data-testid="catalog-concepts">
      <SectionLabel>{`Concepts · ${concepts.length}`}</SectionLabel>
      <input
        data-testid="catalog-concept-filter"
        type="text"
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
        placeholder="filter concepts…"
        className="w-full rounded border border-stone-200 px-2 py-1 dsh-mono text-stone-700 focus:outline-none focus:ring-1 focus:ring-stone-300"
      />
      <ul className="space-y-1">
        {shown.map((c) => (
          <li key={c.name} data-testid={`catalog-concept-${c.name}`} className="font-mono dsh-mono">
            <span className="font-semibold text-stone-800">{c.name}</span>
            <span className="ml-1.5 text-emerald-700">{c.type.toLowerCase()}</span>
            <span className="text-stone-500"> — {c.description}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}
