import { use } from "react";
import { AccessStudio } from "@/components/access/AccessStudio";
import { ConnectionsView } from "@/components/system/ConnectionsView";
import { InstallationIdentityView } from "@/components/system/InstallationIdentityView";
import { DataArchiveView } from "@/components/system/DataArchiveView";
import { ConfigStudio, type StudioSection } from "@/components/config/ConfigStudio";
import { SettingsStudio } from "@/components/config/SettingsStudio";
import { isConfigEntityKind } from "@/components/config/entities";
import { SETTING_KEYS, isSettingKey } from "@/components/config/settings";

// p0345: the Configuration studio route. The optional catch-all slug selects the
// section — /config → agents (default), /config/{kind} → that catalog,
// /config/changes → the audit view — so selection is URL-stable and deep-linkable,
// mirroring the /system route's slug-driven master/detail.
// p0353: /config/settings/{key} → the global settings singleton's typed form (a bare
// /config/settings falls to the first key), rendered inside the same studio shell.
// 2026-08-27-1ed6: /config/installation → what this installation runs, and
// /config/connection-check → whether its dependencies answer. Both used to hang off
// /system, where the running system is watched; an installation is not a subsystem of
// itself, and keeping them there would have forced the rail to learn a list of routes
// that count as configuration. The check is NOT at /config/connections — that path is
// the connection catalog, which is a different question about the same word.

interface PageProps {
  params: Promise<{ slug?: string[] }>;
}

function sectionFromSlug(slug?: string[]): StudioSection {
  const seg = slug?.[0];
  if (seg === "changes") return "changes";
  if (isConfigEntityKind(seg)) return seg;
  return "agents";
}

export default function ConfigPage({ params }: PageProps) {
  const { slug } = use(params);
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
  if (slug?.[0] === "settings") {
    const key = isSettingKey(slug[1]) ? slug[1] : SETTING_KEYS[0];
    return <SettingsStudio settingKey={key} />;
  }
  return <ConfigStudio section={sectionFromSlug(slug)} />;
}

// The two moved read-outs render exactly the DOM they rendered under /system — the same
// parity page scope, so this phase changed where they live and nothing they show.
function DiagnosticPage({ children }: { children: React.ReactNode }) {
  return (
    <div className="mock-shell mock-system mock-diagnostic" data-testid="diagnostic-page">
      <main className="main">{children}</main>
    </div>
  );
}
