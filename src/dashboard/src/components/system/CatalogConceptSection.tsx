"use client";

import { useMemo, useState } from "react";
import type { CatalogConcept } from "@/lib/catalogApi";
import { useT } from "@/hooks/useT";

// p0343d: the concept vocabulary as the mock's .lrow log rows behind its filter input.

export function CatalogConceptSection({ concepts }: { concepts: CatalogConcept[] }) {
  const t = useT();
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
        <h2>{t("system.catalog.concepts")}</h2>
        <span className="cnt">{concepts.length}</span>
        <span className="sh-sub">{t("system.catalog.conceptsSub")}</span>
      </div>
      <div style={{ height: 14 }} />
      <input
        data-testid="catalog-concept-filter"
        className="flt mono"
        type="text"
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
        placeholder={t("system.catalog.filterPlaceholder")}
      />
      <div style={{ height: 12 }} />
      {shown.length === 0 ? (
        <div className="stateline" data-testid="catalog-concepts-nomatch">
          {t("system.catalog.noMatch", { query: filter.trim() })}
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
