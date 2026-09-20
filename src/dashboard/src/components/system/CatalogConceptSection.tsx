"use client";

import { useMemo, useState } from "react";
import type { CatalogConcept } from "@/lib/catalogApi";

// p0343d: the concept vocabulary as the mock's .lrow log rows behind its filter input.

export function CatalogConceptSection({ concepts }: { concepts: CatalogConcept[] }) {
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
