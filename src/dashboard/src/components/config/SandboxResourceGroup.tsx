"use client";

import type { InheritedResources, ResourceSummary, SandboxResourceLayer } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";

// 2026-09-22-6c46: the cpu/memory group. Its resolution has FOUR layers — the project's own
// block, a light profile forced on every pipeline that changes no code, resources an LLM
// wrote into the repository's context document, and the process-wide default — so the one
// thing this control cannot honestly do is print a single inherited number and call it what
// the project would get. It names the LAYER that would answer instead.
//
// And it is wholly given or wholly inherited: the model refuses a partial override (all four
// quantities are required), so there is no state in which two of them are the project's and
// two are not. Switching the group on seeds all four from what it would inherit; emptying
// one puts that quantity back, because a blank is not one of this group's options — the way
// to say "inherit" is to switch the whole group off.

const FIELDS = [
  { key: "cpuRequest", label: "config.resources.cpuRequest" },
  { key: "cpuLimit", label: "config.resources.cpuLimit" },
  { key: "memoryRequest", label: "config.resources.memoryRequest" },
  { key: "memoryLimit", label: "config.resources.memoryLimit" },
] as const;

// 2026-10-12-006d: the layer is the answer for a run that CHANGES CODE. A project's runs take
// their pipeline per run — a ticket's labels, a trigger's default, the CLI — so its own
// pipeline field decides nothing, and the light profile a run that changes no code is held to
// is named beside it rather than in place of it.
const LAYER_SENTENCE = {
  "project-override": "config.resources.layer.projectOverride",
  "light-profile": "config.resources.layer.lightProfile",
  "context-document": "config.resources.layer.contextDocument",
  "global-default": "config.resources.layer.globalDefault",
} as const satisfies Record<SandboxResourceLayer, string>;

function layerSentence(t: T, inherited: InheritedResources): string {
  const own = t("config.resources.layer.own", {
    sentence: t(LAYER_SENTENCE[inherited.layer]), values: summarise(t, inherited.values),
  });
  if (inherited.layer !== "global-default" && inherited.layer !== "context-document") return own;
  return t("config.resources.layer.withNoCode", { own, values: summarise(t, inherited.noCodeValues) });
}

export function SandboxResourceGroup({
  value,
  inherited,
  onChange,
}: {
  value: ResourceSummary | null | undefined;
  inherited: InheritedResources | undefined;
  onChange: (next: ResourceSummary | undefined) => void;
}) {
  const t = useT();
  const given = value ?? null;
  const fallback = inherited?.values;

  const set = (key: keyof ResourceSummary, raw: string) => {
    if (given === null) return;
    const typed = raw.trim();
    onChange({ ...given, [key]: typed === "" ? (fallback?.[key] ?? given[key]) : typed });
  };

  return (
    <div className="field" data-testid="form-field-sandbox-resources">
      <label>
        {t("config.resources.label")}
        <span className="help">
          {t("config.resources.help")}
        </span>
      </label>

      <p className="help" data-testid="form-sandbox-resources-layer">
        {inherited ? layerSentence(t, inherited) : t("config.resources.layerUnreadable")}
      </p>

      {/* With no inherited values there is nothing to seed all four from, and this group
          has no half-state: the only alternative would be four blanks, which a sandbox
          spawn would carry as unparseable quantities. */}
      {given === null ? (
        <button
          type="button"
          className="pick"
          data-testid="form-field-sandbox-resources-override"
          disabled={fallback === undefined}
          onClick={() => fallback && onChange({ ...fallback })}
        >
          {fallback === undefined
            ? t("config.resources.cannotSize")
            : t("config.resources.sizeMyself")}
        </button>
      ) : (
        <>
          <div className="maprows">
            {FIELDS.map((f) => (
              <div className="maprow" key={f.key}>
                <input
                  type="text"
                  className="mono"
                  aria-label={t(f.label)}
                  data-testid={`form-field-sandbox-resources-${f.key}`}
                  value={given[f.key]}
                  placeholder={fallback?.[f.key]}
                  onChange={(e) => set(f.key, e.target.value)}
                />
                <span className="help">{t(f.label)}</span>
              </div>
            ))}
          </div>
          <button
            type="button"
            className="pick"
            data-testid="form-field-sandbox-resources-inherit"
            onClick={() => onChange(undefined)}
          >
            {t("config.resources.inherit")}
          </button>
        </>
      )}
    </div>
  );
}


const summarise = (t: T, r: ResourceSummary) =>
  t("config.resources.summary", { cpuRequest: r.cpuRequest, cpuLimit: r.cpuLimit, memoryRequest: r.memoryRequest, memoryLimit: r.memoryLimit });
