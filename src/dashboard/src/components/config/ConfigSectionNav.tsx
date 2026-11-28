"use client";

import { usePathname } from "next/navigation";
import { useConfigCatalogContext } from "@/components/config/ConfigCatalogProvider";
import { ENTITY_LABEL_KEY } from "@/components/config/entities";
import { SETTINGS_NAV, settingsNavHref, settingsNavIcon, settingsNavLabel } from "@/components/config/settings";
import { SectionNav, SectionNavGroup } from "@/components/shell/SectionNav";
import { SectionNavItem } from "@/components/shell/SectionNavItem";
import type { ConfigEntityKind } from "@/lib/configApi";
import { useT } from "@/hooks/useT";

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
  const t = useT();
  const pathname = usePathname() ?? "";
  const { catalog, loading, changesCount } = useConfigCatalogContext();
  const at = (href: string) => pathname === href;

  return (
    <SectionNav label={t("config.nav.settings")} testId="config-nav">
      <SectionNavGroup label={t("config.nav.catalog")} />
      {CATALOG_KINDS.map(({ kind, icon }) => (
        <SectionNavItem
          key={kind}
          label={t(ENTITY_LABEL_KEY[kind])}
          href={`/config/${kind}`}
          icon={icon}
          active={at(`/config/${kind}`) || (kind === "agents" && at("/config"))}
          count={loading ? undefined : catalog[kind].length}
        />
      ))}
      <SectionNavGroup label={t("config.nav.settings")} />
      {/* 2026-11-28-9bd1: three groups of the small limit and cadence pages, beside the pages of their own. */}
      {SETTINGS_NAV.map((entry) => {
        const href = settingsNavHref(entry);
        return (
          <SectionNavItem
            key={href}
            label={t(settingsNavLabel(entry))}
            href={href}
            icon={settingsNavIcon(entry)}
            active={at(href) || (entry === SETTINGS_NAV[0] && at("/config/settings"))}
          />
        );
      })}
      {/* 2026-08-26-7a51: its own group, because it needs access.read/access.write and not
          config.read/config.write. */}
      <SectionNavGroup label={t("config.nav.access")} />
      <SectionNavItem label={t("config.nav.permissions")} href="/config/access" icon="◈" active={at("/config/access")} />
      <SectionNavGroup label={t("config.nav.thisInstallation")} />
      <SectionNavItem label={t("config.nav.installation")} href="/config/installation" icon="⬡" active={at("/config/installation")} />
      <SectionNavItem label={t("config.nav.connectionCheck")} href="/config/connection-check" icon="◳" active={at("/config/connection-check")} />
      <SectionNavItem
        label={t("config.nav.changes")}
        href="/config/changes"
        icon="◔"
        active={at("/config/changes")}
        count={changesCount ?? undefined}
      />
    </SectionNav>
  );
}
