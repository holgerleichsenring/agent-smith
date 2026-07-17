"use client";

import { useMemo, useState } from "react";
import type { CatalogConcept, CatalogContents, CatalogEntry } from "@/lib/catalogApi";
import { Markdown } from "@/components/ui/Markdown";

// p0221: the catalog browser's presentational layer. Masters/skills render as
// cards whose SKILL.md body is fetched lazily on expand; concepts render as
// filterable rows showing name + type + definition. Takes the contents + a
// body loader as props so it is unit-testable without the fetch layer.
// p0343d: parity re-dress — inventory as the mock's .ecard cards (icon block,
// mono name, description sub-line, role badge, whole top row toggles), concept
// vocabulary as .lrow log rows behind the mock filter input, .section-head
// rules with .cnt counts between the groups.

interface CatalogBrowserViewProps {
  contents: CatalogContents;
  loadBody: (name: string) => Promise<string | null>;
}

export function CatalogBrowserView({ contents, loadBody }: CatalogBrowserViewProps) {
  return (
    <div data-testid="catalog-browser">
      <EntrySection label="Masters" icon="✦" entries={contents.masters} loadBody={loadBody} />
      <EntrySection label="Skills" icon="◆" entries={contents.skills} loadBody={loadBody} />
      <ConceptSection concepts={contents.concepts} />
    </div>
  );
}

function EntrySection({
  label,
  icon,
  entries,
  loadBody,
}: {
  label: string;
  icon: string;
  entries: CatalogEntry[];
  loadBody: (name: string) => Promise<string | null>;
}) {
  if (entries.length === 0) return null;
  return (
    <section>
      <div className="section-head">
        <h2>{label}</h2>
        <span className="cnt">{entries.length}</span>
        <span className="sh-sub">expand a card to read its SKILL.md</span>
      </div>
      <div style={{ height: 14 }} />
      <div className="list">
        {entries.map((entry) => (
          <EntryCard key={entry.name} entry={entry} icon={icon} loadBody={loadBody} />
        ))}
      </div>
    </section>
  );
}

function EntryCard({
  entry,
  icon,
  loadBody,
}: {
  entry: CatalogEntry;
  icon: string;
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
          <span className="edit-hint">{open ? "close ▴" : "open ▾"}</span>
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
        </div>
      )}
    </div>
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
    <section data-testid="catalog-concepts">
      <div className="section-head">
        <h2>Concepts</h2>
        <span className="cnt">{concepts.length}</span>
        <span className="sh-sub">the vocabulary skills declare their findings in</span>
      </div>
      <div style={{ height: 14 }} />
      <input
        data-testid="catalog-concept-filter"
        className="flt mono"
        type="text"
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
        placeholder="filter concepts…"
      />
      <div style={{ height: 12 }} />
      {shown.length === 0 ? (
        <div className="stateline" data-testid="catalog-concepts-nomatch">
          No concept matches “{filter.trim()}”.
        </div>
      ) : (
        <div className="rows">
          {shown.map((c) => (
            <div key={c.name} data-testid={`catalog-concept-${c.name}`} className="lrow">
              <span className="id">{c.name}</span>
              <span>{c.description}</span>
              <span className="meta">{c.type.toLowerCase()}</span>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
