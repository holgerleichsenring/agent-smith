import { use } from "react";
import { redirect } from "next/navigation";
import { AccessStudio } from "@/components/access/AccessStudio";
import { ConnectionsView } from "@/components/system/ConnectionsView";
import { InstallationIdentityView } from "@/components/system/InstallationIdentityView";
import { DataArchiveView } from "@/components/system/DataArchiveView";
import { ConfigStudio, type StudioSection } from "@/components/config/ConfigStudio";
import { SettingsStudio } from "@/components/config/SettingsStudio";
import { isConfigEntityKind } from "@/components/config/entities";
import { SettingsGroupStudio } from "@/components/config/SettingsGroupStudio";
import {
  SETTINGS_NAV,
  groupOf,
  isSettingGroup,
  isSettingKey,
  settingHref,
  settingsNavHref,
} from "@/components/config/settings";

// p0345: the Configuration studio route. The optional catch-all slug selects the
// section — /config → agents (default), /config/{kind} → that catalog,
// /config/changes → the audit view — so selection is URL-stable and deep-linkable,
// mirroring the /system route's slug-driven master/detail.
// p0353: /config/settings/{key} → the global settings singleton's typed form, rendered inside the
// same studio shell (2026-11-28-9bd1: or a group of them — see SettingsPage).
// 2026-08-27-1ed6: /config/installation → what this installation runs, and
// /config/connection-check → whether its dependencies answer. Both used to hang off
// /system, where the running system is watched; an installation is not a subsystem of
// itself, and keeping them there would have forced the rail to learn a list of routes
// that count as configuration. The check is NOT at /config/connections — that path is
// the connection catalog, which is a different question about the same word.

interface PageProps {
  params: Promise<{ slug?: string[] }>;
  /** 2026-11-28-9bd1: ?open={key} names the section of a settings group to open and scroll to. */
  searchParams?: Promise<{ open?: string | string[] }>;
}

function sectionFromSlug(slug?: string[]): StudioSection {
  const seg = slug?.[0];
  if (seg === "changes") return "changes";
  if (isConfigEntityKind(seg)) return seg;
  return "agents";
}

export default function ConfigPage({ params, searchParams }: PageProps) {
  const { slug } = use(params);
  const query = searchParams ? use(searchParams) : {};
  // 2026-08-26-7a51: /config/access → who may do what. Its own surface and its own
  // permission, because config.write must not be enough to grant somebody admin.
  if (slug?.[0] === "access") return <AccessStudio />;
  if (slug?.[0] === "installation") {
    return (
      <DiagnosticPage>
        {/* 2026-08-28-3793: the archive is a fact ABOUT this installation, so it sits with
            the versions and the database state rather than in the config catalog. It loads
            on its own: a read-out that cannot answer must not take the other one with it. */}
        <InstallationIdentityView />
        <DataArchiveView />
      </DiagnosticPage>
    );
  }
  if (slug?.[0] === "connection-check") {
    return (
      <DiagnosticPage>
        <ConnectionsView />
      </DiagnosticPage>
    );
  }
  if (slug?.[0] === "settings") return <SettingsPage segment={slug[1]} open={query.open} />;
  return <ConfigStudio section={sectionFromSlug(slug)} />;
}

// 2026-11-28-9bd1: /config/settings/{group} is a group of sections, /config/settings/{key} a page of
// its own. A key that became a section redirects to its group with that section open, so a bookmark
// or a link in a ticket still lands on it; a bare or unknown segment goes to the first rail entry.
function SettingsPage({ segment, open }: { segment?: string; open?: string | string[] }) {
  if (isSettingGroup(segment)) {
    const key = Array.isArray(open) ? open[0] : open;
    return <SettingsGroupStudio key={segment} groupId={segment} open={isSettingKey(key) ? key : null} />;
  }
  if (isSettingKey(segment)) {
    if (groupOf(segment)) return redirect(settingHref(segment));
    return <SettingsStudio settingKey={segment} />;
  }
  return redirect(settingsNavHref(SETTINGS_NAV[0]));
}

// The two moved read-outs render exactly the DOM they rendered under /system — the same
// parity page scope, so this spec changed where they live and nothing they show.
function DiagnosticPage({ children }: { children: React.ReactNode }) {
  return (
    <div className="mock-shell mock-system mock-diagnostic" data-testid="diagnostic-page">
      <main className="main">{children}</main>
    </div>
  );
}
