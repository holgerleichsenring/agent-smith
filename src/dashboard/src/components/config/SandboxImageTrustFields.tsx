"use client";

import type { SandboxSetting } from "@/lib/configApi";
import { DrawerSection } from "./formFields";
import { ImageTrustInForce } from "./ImageTrustInForce";
import { useImageTrust } from "./useImageTrust";

// 2026-10-12-6bfab: the supply-chain boundary for a context's LLM-named stack.image —
// sandbox.allowed_registries and sandbox.allow_docker_hub_library — editable where the rest
// of the sandbox block is, beside the boundary the server says is in force.

type Library = "follow" | "trust" | "refuse";

export function libraryChoice(value: boolean | null | undefined): Library {
  if (value === true) return "trust";
  if (value === false) return "refuse";
  return "follow";
}

export function librarySetting(choice: Library): boolean | null {
  if (choice === "trust") return true;
  if (choice === "refuse") return false;
  return null;
}

/** The list after "Add a registry": the first entry starts from the built-in default, so
 *  naming one registry does not silently drop the ones that were trusted until now. */
export function withAddedRegistry(list: string[], defaults: string[]): string[] {
  return list.length === 0 ? [...defaults, ""] : [...list, ""];
}

export function SandboxImageTrustFields({
  value,
  onChange,
  persisted,
}: {
  value: SandboxSetting;
  onChange: (patch: Partial<SandboxSetting>) => void;
  persisted: SandboxSetting | null;
}) {
  const list = value.allowedRegistries ?? [];
  const saved = JSON.stringify([persisted?.allowedRegistries ?? [], persisted?.allowDockerHubLibrary ?? null]);
  const draft = JSON.stringify([list, value.allowDockerHubLibrary ?? null]);
  const { boundary, error } = useImageTrust(saved);
  const defaults = boundary?.defaultRegistries ?? [];
  const setList = (next: string[]) => onChange({ allowedRegistries: next });

  return (
    <DrawerSection title="Trusted image registries" summary={list.length ? `${list.length} named` : "default"}
      defaultOpen testId="setting-sandbox-trust">
      <p className="help" data-testid="setting-sandbox-trust-note">
        Where a context&apos;s stack.image may come from. Each entry is a prefix of the image
        reference as Docker reads it — an image with no registry host is on docker.io, so
        sbtscala/ and docker.io/sbtscala/ are the same entry, and docker.io/ alone trusts all of
        Docker Hub. Only the stack.image a context declares is judged: an image named in a
        project&apos;s sandbox settings and the built-in fallbacks are not. A refused image falls back
        to the language default. Applies to the next decision, no restart.
      </p>
      {list.length === 0 && (
        <p className="help" data-testid="setting-sandbox-trust-empty">
          No registry named — the built-in default applies{defaults.length ? `: ${defaults.join(", ")}` : ""}.
          Adding the first entry starts from it.
        </p>
      )}
      {list.map((entry, i) => (
        <div key={i} className="field" style={{ display: "flex", gap: 8 }}>
          <input type="text" className="mono" value={entry} placeholder="docker.io/sbtscala/"
            data-testid={`setting-sandbox-trust-${i}`}
            onChange={(e) => setList(list.map((r, j) => (j === i ? e.target.value : r)))} />
          <button type="button" className="btn" data-testid={`setting-sandbox-trust-${i}-remove`}
            onClick={() => setList(list.filter((_, j) => j !== i))} style={{ color: "var(--bad)" }}>
            Remove
          </button>
        </div>
      ))}
      <button type="button" className="btn" data-testid="setting-sandbox-trust-add"
        onClick={() => setList(withAddedRegistry(list, defaults))}>
        Add a registry
      </button>
      <div className="field">
        <label>
          Docker Hub official library images
          <span className="help">
            node:20-bookworm, buildpack-deps — following the list trusts them while no registry is
            named and refuses them once one is
          </span>
        </label>
        <select className="mono" data-testid="setting-sandbox-trust-library"
          value={libraryChoice(value.allowDockerHubLibrary)}
          onChange={(e) => onChange({ allowDockerHubLibrary: librarySetting(e.target.value as Library) })}>
          <option value="follow">Follow the registry list</option>
          <option value="trust">Trust</option>
          <option value="refuse">Refuse</option>
        </select>
      </div>
      <ImageTrustInForce boundary={boundary} error={error} dirty={draft !== saved} />
    </DrawerSection>
  );
}
