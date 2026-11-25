"use client";

import type { RegistryEntry } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { DrawerSection, TextField } from "./formFields";

// 2026-11-25-9992d: a project's private package feeds — host, username and the token's secret reference. Moved here
// from the installation Settings, which no longer hold feeds: each project names its own.

export function RegistryFeedsField({
  value,
  onChange,
}: {
  value: readonly RegistryEntry[] | null | undefined;
  onChange: (next: RegistryEntry[]) => void;
}) {
  const t = useT();
  const feeds = value ?? [];
  const setAt = (i: number, patch: Partial<RegistryEntry>) =>
    onChange(feeds.map((r, j) => (j === i ? { ...r, ...patch } : r)));
  const remove = (i: number) => onChange(feeds.filter((_, j) => j !== i));
  const add = () => onChange([...feeds, { host: "", username: "", token: "" }]);

  return (
    <div className="field" data-testid="form-field-registries">
      <label>
        {t("config.registries.label")} <span className="help">{t("config.registries.help")}</span>
      </label>
      {feeds.length === 0 && <span className="help">{t("config.registries.none")}</span>}
      {feeds.map((entry, i) => (
        <DrawerSection key={i} title={entry.host || t("config.registries.feed", { n: i + 1 })} defaultOpen
          testId={`form-registry-${i}`}>
          <TextField label={t("config.registries.host")} value={entry.host} onChange={(v) => setAt(i, { host: v })} mono
            placeholder="pkgs.dev.azure.com" testId={`form-registry-${i}-host`} />
          <TextField label={t("config.registries.username")} value={entry.username} onChange={(v) => setAt(i, { username: v })} mono
            placeholder={t("config.registries.usernamePlaceholder")} testId={`form-registry-${i}-username`} />
          <TextField label={t("config.registries.token.label")} value={entry.token} onChange={(v) => setAt(i, { token: v })} mono
            placeholder="${feed_token}" testId={`form-registry-${i}-token`}
            help={t("config.registries.token.help")} />
          <button type="button" className="btn" onClick={() => remove(i)}
            data-testid={`form-registry-${i}-remove`} style={{ color: "var(--bad)" }}>
            {t("config.registries.remove")}
          </button>
        </DrawerSection>
      ))}
      <button type="button" className="btn" onClick={add} data-testid="form-registry-add">
        {t("config.registries.add")}
      </button>
    </div>
  );
}
