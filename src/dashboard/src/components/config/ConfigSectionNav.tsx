"use client";

import { usePathname } from "next/navigation";
import { useConfigCatalogContext } from "@/components/config/ConfigCatalogProvider";
import { ENTITY_LABEL } from "@/components/config/entities";
import { SETTING_ICON, SETTING_KEYS, SETTING_LABEL } from "@/components/config/settings";
import { SectionNav, SectionNavGroup } from "@/components/shell/SectionNav";
import { SectionNavItem } from "@/components/shell/SectionNavItem";
import type { ConfigEntityKind } from "@/lib/configApi";

// p0343b: the configuration sections — the entity CATALOG with live counts (the same list clients
// the studio loads), the global SETTINGS singletons, ACCESS and THIS INSTALLATION with the Changes
// count. 2026-11-16-0440c: moved out of the app rail into the /config layout, beside its pages.
// p0353: the counts are the SHARED catalog the studio edits, so an import/save/revert refreshes
// them too.

const CATALOG_KINDS: Array<{ kind: ConfigEntityKind; icon: string }> = [
  { kind: "projects", icon: "◈" },
  { kind: "agents", icon: "✦" },
  { kind: "trackers", icon: "◱" },
  { kind: "repos", icon: "⎇" },
  { kind: "connections", icon: "◳" },
  { kind: "mcp-servers", icon: "⇄" },
  { kind: "design-sources", icon: "◩" },
  { kind: "secrets", icon: "◍" },
];

export function ConfigSectionNav() {
  const pathname = usePathname() ?? "";
  const { catalog, loading, changesCount } = useConfigCatalogContext();
  const at = (href: string) => pathname === href;

  return (
    <SectionNav label="Settings" testId="config-nav">
      <SectionNavGroup label="Catalog" />
      {CATALOG_KINDS.map(({ kind, icon }) => (
        <SectionNavItem
          key={kind}
          label={ENTITY_LABEL[kind]}
          href={`/config/${kind}`}
          icon={icon}
          active={at(`/config/${kind}`) || (kind === "agents" && at("/config"))}
          count={loading ? undefined : catalog[kind].length}
        />
      ))}
      <SectionNavGroup label="Settings" />
      {SETTING_KEYS.map((key) => (
        <SectionNavItem
          key={key}
          label={SETTING_LABEL[key]}
          href={`/config/settings/${key}`}
          icon={SETTING_ICON[key]}
          active={at(`/config/settings/${key}`)}
        />
      ))}
      {/* 2026-08-26-7a51: its own group, because it needs access.read/access.write and not
          config.read/config.write. */}
      <SectionNavGroup label="Access" />
      <SectionNavItem label="Permissions" href="/config/access" icon="◈" active={at("/config/access")} />
      <SectionNavGroup label="This installation" />
      <SectionNavItem label="Installation" href="/config/installation" icon="⬡" active={at("/config/installation")} />
      <SectionNavItem label="Connection check" href="/config/connection-check" icon="◳" active={at("/config/connection-check")} />
      <SectionNavItem
        label="Changes"
        href="/config/changes"
        icon="◔"
        active={at("/config/changes")}
        count={changesCount ?? undefined}
      />
    </SectionNav>
  );
}
