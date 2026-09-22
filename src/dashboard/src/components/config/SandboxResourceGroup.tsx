"use client";

import type { InheritedResources, ResourceSummary, SandboxResourceLayer } from "@/lib/configApi";

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
  { key: "cpuRequest", label: "cpu request" },
  { key: "cpuLimit", label: "cpu limit" },
  { key: "memoryRequest", label: "memory request" },
  { key: "memoryLimit", label: "memory limit" },
] as const;

const LAYER_SENTENCE: Record<SandboxResourceLayer, string> = {
  "project-override": "this project's own block answers",
  "light-profile":
    "this project's pipeline changes no code, so the fixed light profile answers — the process-wide default never reaches it",
  "context-document":
    "the resources the repository's context document names answer, clamped to the operator ceiling",
  "global-default":
    "the process-wide sandbox default answers, unless the repository's context document names resources for that run",
};

export function SandboxResourceGroup({
  value,
  inherited,
  onChange,
}: {
  value: ResourceSummary | null | undefined;
  inherited: InheritedResources | undefined;
  onChange: (next: ResourceSummary | undefined) => void;
}) {
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
        cpu &amp; memory
        <span className="help">
          all four quantities or none — a partial override is not a shape this product has
        </span>
      </label>

      <p className="help" data-testid="form-sandbox-resources-layer">
        {inherited
          ? `${LAYER_SENTENCE[inherited.layer]} (${summarise(inherited.values)})`
          : "the layer that would answer could not be read"}
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
            ? "cannot size this project while the inherited values are unreadable"
            : "+ size this project myself"}
        </button>
      ) : (
        <>
          <div className="maprows">
            {FIELDS.map((f) => (
              <div className="maprow" key={f.key}>
                <input
                  type="text"
                  className="mono"
                  aria-label={f.label}
                  data-testid={`form-field-sandbox-resources-${f.key}`}
                  value={given[f.key]}
                  placeholder={fallback?.[f.key]}
                  onChange={(e) => set(f.key, e.target.value)}
                />
                <span className="help">{f.label}</span>
              </div>
            ))}
          </div>
          <button
            type="button"
            className="pick"
            data-testid="form-field-sandbox-resources-inherit"
            onClick={() => onChange(undefined)}
          >
            × hand the whole group back to what it inherits
          </button>
        </>
      )}
    </div>
  );
}


const summarise = (r: ResourceSummary) =>
  `cpu ${r.cpuRequest}/${r.cpuLimit}, memory ${r.memoryRequest}/${r.memoryLimit}`;
