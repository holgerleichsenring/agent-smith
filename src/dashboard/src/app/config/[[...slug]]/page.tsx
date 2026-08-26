import { use } from "react";
import { AccessStudio } from "@/components/access/AccessStudio";
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
  if (slug?.[0] === "settings") {
    const key = isSettingKey(slug[1]) ? slug[1] : SETTING_KEYS[0];
    return <SettingsStudio settingKey={key} />;
  }
  return <ConfigStudio section={sectionFromSlug(slug)} />;
}
