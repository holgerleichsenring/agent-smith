import type { SandboxSetting, SettingKey, SettingValue } from "@/lib/configApi";
import type { T } from "@/lib/i18n/translate";

// p0495: what the studio refuses to save, and why, in the operator's own numbers.
//
// The sandbox timeouts are one rule, not two independent knobs: a run_command gets the
// default when it asks for nothing, may ask for more, and is killed at the step cap.
// A default above the cap is therefore a number that can never apply — the cap only ever
// LOWERS a step's timeout. Two settings that contradict each other is how the operator
// came to configure 900 and watch a command die at 600, so the refusal names both values
// rather than reporting a generic "invalid".

export function settingRefusal(t: T, settingKey: SettingKey, value: SettingValue): string | null {
  if (settingKey !== "sandbox") return null;
  const sandbox = value as SandboxSetting;
  const runCommand = sandbox.runCommandTimeoutSeconds;
  const cap = sandbox.stepTimeoutSeconds;
  if (!(runCommand > cap)) return null;
  return t("config.refusal.runCommandOverCap", { runCommand, cap });
}
