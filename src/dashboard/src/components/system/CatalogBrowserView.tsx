"use client";

import type { CatalogContents, CatalogEntry } from "@/lib/catalogApi";
import { CatalogOriginLine } from "./CatalogOriginLine";
import { CatalogEntryCard } from "./CatalogEntryCard";
import { CatalogConceptSection } from "./CatalogConceptSection";
import { useT } from "@/hooks/useT";

// p0221: the catalog browser's presentational layer. Masters/skills render as
// cards whose SKILL.md body is fetched lazily on expand; concepts render as
// filterable rows showing name + type + definition. Takes the contents + a
// body loader as props so it is unit-testable without the fetch layer.
// 2026-09-18-84be: the page names the ORIGIN the server resolved — the resolution's own
// phrase, not a second one minted here — and says where a change to the text is made:
// the Skills settings singleton in this same dashboard, or, on an overlaid installation,
// a directory no form in this dashboard carries. Both sentences come from one component.
// p0343d: parity re-dress — inventory as the mock's .ecard cards (icon block,
// mono name, description sub-line, role badge, whole top row toggles), concept
// vocabulary as .lrow log rows behind the mock filter input, .section-head
// rules with .cnt counts between the groups.

interface CatalogBrowserViewProps {
  contents: CatalogContents;
  loadBody: (name: string) => Promise<string | null>;
}

export function CatalogBrowserView({ contents, loadBody }: CatalogBrowserViewProps) {
  const t = useT();
  const overlayPath = contents.origin?.overlayPath ?? null;
  return (
    <div data-testid="catalog-browser">
      <CatalogOriginLine origin={contents.origin} />
      <EntrySection
        label={t("system.catalog.masters")}
        icon="✦"
        entries={contents.masters}
        loadBody={loadBody}
        overlayPath={overlayPath}
      />
      <EntrySection
        label={t("system.catalog.skills")}
        icon="◆"
        entries={contents.skills}
        loadBody={loadBody}
        overlayPath={overlayPath}
      />
      <CatalogConceptSection concepts={contents.concepts} />
    </div>
  );
}

function EntrySection({
  label,
  icon,
  entries,
  loadBody,
  overlayPath,
}: {
  label: string;
  icon: string;
  entries: CatalogEntry[];
  loadBody: (name: string) => Promise<string | null>;
  overlayPath: string | null;
}) {
  const t = useT();
  if (entries.length === 0) return null;
  return (
    <section>
      <div className="section-head">
        <h2>{label}</h2>
        <span className="cnt">{entries.length}</span>
        <span className="sh-sub">{t("system.catalog.expandHint")}</span>
      </div>
      <div style={{ height: 14 }} />
      <div className="list">
        {entries.map((entry) => (
          <CatalogEntryCard
            key={entry.name}
            entry={entry}
            icon={icon}
            loadBody={loadBody}
            overlayPath={overlayPath}
          />
        ))}
      </div>
    </section>
  );
}
