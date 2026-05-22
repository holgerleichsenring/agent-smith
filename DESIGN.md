---
version: alpha
name: AgentSmith-design-system
description: Agent Smith's design language — workflow-automation aesthetics tuned for a self-hosted developer-infrastructure tool. Warm-cream canvas (`{colors.canvas}` `#fffefb`), deep coffee ink (`{colors.ink}` `#201515`), and a single saturated brand-green (`{colors.primary}` `#22c55e`) as the sole CTA accent. Typography ladders Inter exclusively — display weight 500 at hero scale, 400 / 500 / 600 / 700 for everything else. Mono surfaces use IBM Plex Mono. Buttons and cards share `{rounded.md}` (12 px) — neither pills nor squares. A polarity-flipped dark variant ships in-system: every hero, card, and pricing tier has a `*-dark` sibling so dark surfaces are content choices, not separate-stylesheet contortions. Plus one Smith-specific component the source pattern doesn't carry — `card-terminal-panel`, a dark-ink monospace panel for CLI output, YAML snippets, and code samples that the rest of the system passes over silently.

colors:
  primary: "#22c55e"
  primary-pressed: "#16a34a"
  primary-deep: "#15803d"
  on-primary: "#fffefb"
  ink: "#201515"
  ink-soft: "#2f2a26"
  ink-mid: "#36342e"
  body: "#605d52"
  body-mid: "#939084"
  mute: "#c5c0b1"
  canvas: "#fffefb"
  canvas-soft: "#f8f4f0"
  terminal-bg: "#1c1e22"
  terminal-text: "#d8d4cc"
  terminal-accent: "#22c55e"
  terminal-mute: "#6b7280"

typography:
  display-xl:
    fontFamily: "Inter, system-ui, -apple-system, sans-serif"
    fontSize: 56px
    fontWeight: 500
    lineHeight: 56px
  display-lg:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 48px
    fontWeight: 500
    lineHeight: 48px
  display-md:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 32px
    fontWeight: 500
    lineHeight: 36px
    letterSpacing: 1px
  display-sub-lg:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 48px
    fontWeight: 500
    lineHeight: 49.92px
  display-sub-md:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 32px
    fontWeight: 400
    lineHeight: 40px
  display-sub-sm:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 24px
    fontWeight: 600
    lineHeight: 30px
    letterSpacing: -0.6px
  display-xs:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 20px
    fontWeight: 700
    lineHeight: 25px
    letterSpacing: -0.5px
  body-lg:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 20px
    fontWeight: 400
    lineHeight: 30px
    letterSpacing: -0.2px
  body-md:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 18px
    fontWeight: 400
    lineHeight: 27px
  body-md-strong:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 18px
    fontWeight: 600
    lineHeight: 27px
  body-sm:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 16px
    fontWeight: 400
    lineHeight: 24px
  body-sm-strong:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 16px
    fontWeight: 600
    lineHeight: 24px
  caption:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 14px
    fontWeight: 400
    lineHeight: 21px
  eyebrow-uppercase:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 14px
    fontWeight: 500
    lineHeight: 14px
    letterSpacing: 1px
  button-md:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 18px
    fontWeight: 600
    lineHeight: 27px
  button-sm:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: 14.4px
    fontWeight: 700
    lineHeight: 14.4px
    letterSpacing: 0.144px
  mono-code:
    fontFamily: "'IBM Plex Mono', ui-monospace, 'SF Mono', Menlo, monospace"
    fontSize: 14px
    fontWeight: 400
    lineHeight: 21px
  mono-caption:
    fontFamily: "'IBM Plex Mono', ui-monospace, 'SF Mono', Menlo, monospace"
    fontSize: 12px
    fontWeight: 400
    lineHeight: 18px

rounded:
  none: 0px
  sm: 6px
  md: 12px
  pill: 9999px
  full: 9999px

spacing:
  xxs: 2px
  xs: 4px
  sm: 8px
  md: 12px
  lg: 16px
  xl: 24px
  2xl: 32px
  3xl: 48px
  4xl: 64px

components:
  nav-bar:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.ink}"
    typography: "{typography.body-sm}"
    padding: "{spacing.md} {spacing.xl}"
  nav-link:
    textColor: "{colors.ink}"
    typography: "{typography.body-sm}"
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.on-primary}"
    typography: "{typography.button-md}"
    rounded: "{rounded.md}"
    padding: "{spacing.md} {spacing.xl}"
  button-primary-pressed:
    backgroundColor: "{colors.primary-pressed}"
    textColor: "{colors.on-primary}"
  button-secondary:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.on-primary}"
    typography: "{typography.button-md}"
    rounded: "{rounded.md}"
    padding: "{spacing.md} {spacing.xl}"
  button-tertiary:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.ink}"
    borderColor: "{colors.ink}"
    typography: "{typography.button-md}"
    rounded: "{rounded.md}"
    padding: "{spacing.md} {spacing.xl}"
  button-text:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.ink}"
    typography: "{typography.button-sm}"
    rounded: "{rounded.md}"
    padding: "{spacing.sm} {spacing.lg}"
  text-input:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.ink}"
    borderColor: "{colors.ink}"
    typography: "{typography.body-md}"
    rounded: "{rounded.sm}"
    padding: "{spacing.md} {spacing.lg}"
  card-content:
    backgroundColor: "{colors.canvas-soft}"
    textColor: "{colors.ink}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: "{spacing.xl}"
  card-feature-cream:
    backgroundColor: "{colors.canvas-soft}"
    textColor: "{colors.ink}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: "{spacing.xl}"
  card-feature-dark:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.on-primary}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: "{spacing.xl}"
  card-terminal-panel:
    backgroundColor: "{colors.terminal-bg}"
    textColor: "{colors.terminal-text}"
    typography: "{typography.mono-code}"
    rounded: "{rounded.md}"
    padding: "{spacing.xl}"
    accent: "{colors.terminal-accent}"
    mute: "{colors.terminal-mute}"
  pricing-card:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.ink}"
    borderColor: "{colors.ink}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: "{spacing.xl}"
  pricing-card-featured:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.on-primary}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: "{spacing.xl}"
  hero-band:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.ink}"
    typography: "{typography.display-xl}"
    padding: "{spacing.4xl} {spacing.xl}"
  hero-band-dark:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.on-primary}"
    typography: "{typography.display-xl}"
    padding: "{spacing.4xl} {spacing.xl}"
  content-band-cream:
    backgroundColor: "{colors.canvas-soft}"
    textColor: "{colors.ink}"
    typography: "{typography.display-lg}"
    padding: "{spacing.4xl} {spacing.xl}"
  content-band-light:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.ink}"
    typography: "{typography.display-lg}"
    padding: "{spacing.4xl} {spacing.xl}"
  eyebrow-uppercase:
    textColor: "{colors.ink}"
    typography: "{typography.eyebrow-uppercase}"
  badge-pill:
    backgroundColor: "{colors.canvas-soft}"
    textColor: "{colors.ink}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.pill}"
    padding: "{spacing.xs} {spacing.md}"
  badge-pill-green:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.on-primary}"
    typography: "{typography.body-sm-strong}"
    rounded: "{rounded.pill}"
    padding: "{spacing.xs} {spacing.md}"
  workspace-mockup-card:
    backgroundColor: "{colors.canvas}"
    rounded: "{rounded.md}"
    padding: "0"
    border: "1px solid {colors.mute}"
    shadow: "rgba(32, 21, 21, 0.18) 0px 24px 48px -8px"
  footer-region:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.canvas-soft}"
    typography: "{typography.body-sm}"
    padding: "{spacing.3xl} {spacing.xl}"

  # ─── Pipeline-taxonomy tint mapping (Agent Smith) ───
  pipeline-fix-bug:
    description: "fix-bug pipeline card. Cream chrome — the default pipeline."
    backgroundColor: "{colors.canvas-soft}"
    textColor: "{colors.ink}"
  pipeline-add-feature:
    description: "add-feature pipeline. Cream-bold (canvas-soft with stronger border)."
    backgroundColor: "{colors.canvas-soft}"
    textColor: "{colors.ink}"
    borderColor: "{colors.ink}"
  pipeline-security-scan:
    description: "security-scan pipeline. Polarity-flipped dark for severity signal."
    backgroundColor: "{colors.ink}"
    textColor: "{colors.on-primary}"
  pipeline-api-security-scan:
    description: "api-security-scan pipeline. Same dark polarity as security-scan."
    backgroundColor: "{colors.ink}"
    textColor: "{colors.on-primary}"
  pipeline-legal-analysis:
    description: "legal-analysis pipeline. Cream chrome — quieter than security."
    backgroundColor: "{colors.canvas-soft}"
    textColor: "{colors.ink}"
  pipeline-mad-discussion:
    description: "mad-discussion pipeline. Cream chrome."
    backgroundColor: "{colors.canvas-soft}"
    textColor: "{colors.ink}"
  pipeline-init-project:
    description: "init-project pipeline. Cream chrome — bootstrap step."
    backgroundColor: "{colors.canvas-soft}"
    textColor: "{colors.ink}"
  pipeline-autonomous:
    description: "autonomous pipeline. Dark polarity — operator-driven, non-default."
    backgroundColor: "{colors.ink}"
    textColor: "{colors.on-primary}"
  pipeline-skill-manager:
    description: "skill-manager pipeline. Dark polarity — meta/admin surface."
    backgroundColor: "{colors.ink}"
    textColor: "{colors.on-primary}"

  # ─── Illustrative kit-mirror surfaces (re-purposed for Agent Smith) ───
  ex-pricing-tier:
    description: "Deployment-mode card (self-host / Docker / Kubernetes). Re-uses pricing-card chrome."
    backgroundColor: "{colors.canvas-soft}"
    textColor: "{colors.ink}"
    borderColor: "{colors.mute}"
    rounded: "{rounded.md}"
    padding: "{spacing.xl}"
  ex-pricing-tier-featured:
    description: "Featured deployment mode (typically Kubernetes) — polarity-flipped surface."
    backgroundColor: "{colors.ink}"
    textColor: "{colors.on-primary}"
    rounded: "{rounded.md}"
    padding: "{spacing.xl}"
  ex-pipeline-picker:
    description: "The eight-pipeline taxonomy chooser on the landing — grid of pipeline-* tiles."
    backgroundColor: "{colors.canvas-soft}"
    rounded: "{rounded.md}"
    padding: "{spacing.xl}"
  ex-run-summary:
    description: "One-ticket → N-PRs run summary card. Line items per repo with PR link + status."
    backgroundColor: "{colors.canvas}"
    rounded: "{rounded.md}"
    padding: "{spacing.xl}"
    item-divider: "{colors.mute}"
  ex-sandbox-row:
    description: "Per-repo sandbox row in the orchestrator-dashboard mockup. Active state uses brand primary as the indicator."
    backgroundColor: "{colors.canvas}"
    activeIndicator: "{colors.primary}"
    rounded: "{rounded.sm}"
    padding: "{spacing.md} {spacing.lg}"
  ex-data-table-cell:
    description: "Default data-table th + td chrome. Header uses eyebrow typography; body uses body-sm."
    headerBackground: "{colors.canvas-soft}"
    headerTypography: "{typography.caption}"
    bodyTypography: "{typography.body-sm}"
    cellPadding: "{spacing.md} {spacing.lg}"
    rowBorder: "{colors.mute}"
  ex-config-form-card:
    description: "agentsmith.yml editor card. Re-uses feature-card chrome with text-input primitives inside."
    backgroundColor: "{colors.canvas-soft}"
    rounded: "{rounded.md}"
    padding: "{spacing.xl}"
  ex-modal-card:
    description: "Modal dialog surface — same chrome as feature-card with elevated shadow."
    backgroundColor: "{colors.canvas}"
    rounded: "{rounded.md}"
    padding: "{spacing.xl}"
  ex-empty-state-card:
    description: "Empty-state illustration frame (e.g. 'no runs yet')."
    backgroundColor: "{colors.canvas-soft}"
    rounded: "{rounded.md}"
    padding: "{spacing.3xl}"
    captionTypography: "{typography.body-md}"
  ex-toast:
    description: "Toast notification surface — feature-card shape + medium shadow."
    backgroundColor: "{colors.canvas}"
    rounded: "{rounded.md}"
    padding: "{spacing.md} {spacing.lg}"
    typography: "{typography.body-sm}"
---

## Overview

Agent Smith is a self-hosted, multi-repo CI for AI pipelines. The brand voice the design system carries is *confident-warm*: a workflow-automation tool that treats developers as adults. Cream canvas (`{colors.canvas}` `#fffefb`) with deep coffee ink (`{colors.ink}` `#201515`) plus a single saturated `{colors.primary}` (`#22c55e`) CTA accent. The warmth in the neutrals — cream rather than pure white — is the brand's defining temperature signal.

Type carries the second voice. Inter at weight 500 carries hero displays; 400 / 500 / 600 / 700 ladder body, button, eyebrow, and emphasis. IBM Plex Mono carries every code / CLI / YAML surface — the brand's other typographic signal, and the visual signal that this is a tool for people who write code.

Cards are universally `{rounded.md}` 12 px. Buttons share the same 12 px radius — not pills, not square. The brand sits between the friendly-rounded and the technical-square camps with a deliberate middle position.

**Key Characteristics:**
- A single primary CTA color `{colors.primary}` (`#22c55e`) — Smith green. The conversion signature.
- Warm-cream canvas `{colors.canvas}` (`#fffefb`) — not pure white. The temperature IS the brand voice.
- Deep coffee ink `{colors.ink}` (`#201515`) — not pure black. Warmth carries through to text.
- Inter for everything; IBM Plex Mono for code / CLI / YAML.
- `{rounded.md}` 12 px for buttons + cards — the brand's middle-radius signature.
- A muted cream / coffee neutral ladder — `{colors.canvas-soft}`, `{colors.mute}`, `{colors.body-mid}`, `{colors.body}` — every neutral carries warmth, none are cool grey.
- Polarity-flipped `*-dark` variants live in-system (`hero-band-dark`, `card-feature-dark`, `pricing-card-featured`) — dark surfaces are content choices, not separate themes.
- `card-terminal-panel` is the only Smith-specific component the source pattern doesn't carry — dark-ink monospace panel for the code surfaces that are the brand's whole subject matter.

## Colors

### Brand & Accent
- **Smith Green** (`{colors.primary}` — `#22c55e`): The single brand accent. Every primary CTA button, every conversion target, the in-terminal-panel prompt glyph.
- **Smith Green Pressed** (`{colors.primary-pressed}` — `#16a34a`): Active-state warmer green.
- **Smith Green Deep** (`{colors.primary-deep}` — `#15803d`): Deepest green for the rare emphasis case (badges, hover-only treatments).

### Surface
- **Canvas** (`{colors.canvas}` — `#fffefb`): Warm off-white page background.
- **Canvas Soft** (`{colors.canvas-soft}` — `#f8f4f0`): Cream-tinted soft surface for cards and inset regions.
- **Terminal Background** (`{colors.terminal-bg}` — `#1c1e22`): Near-ink with a cool tint for `card-terminal-panel` only. Distinct from `{colors.ink}` so terminal panels read as their own surface class.

### Text
- **Ink** (`{colors.ink}` — `#201515`): Deep coffee — every heading and primary text.
- **Ink Soft** (`{colors.ink-soft}` — `#2f2a26`): Near-black with brown warmth.
- **Ink Mid** (`{colors.ink-mid}` — `#36342e`): Mid-emphasis text.
- **Body** (`{colors.body}` — `#605d52`): Default body text color.
- **Body Mid** (`{colors.body-mid}` — `#939084`): Secondary body / metadata.
- **Mute** (`{colors.mute}` — `#c5c0b1`): Lowest-priority text — fine print, low-emphasis captions, table dividers.
- **Terminal Text** (`{colors.terminal-text}` — `#d8d4cc`): Warm-off-white for monospace body inside terminal panels.
- **Terminal Mute** (`{colors.terminal-mute}` — `#6b7280`): Faded comment color inside terminal panels.

### Semantic
The brand doesn't surface a separate semantic palette on its marketing pages. Status / validation cues borrow from the ink + green hierarchy: green for success, ink for warning-by-emphasis, the rare `#dc2626` (not tokenised) reserved for hard validation errors inside `text-input` red borders.

## Typography

### Font Family
Two faces ladder the system:
1. **Inter** — used for displays, sub-displays, body, links, buttons, and eyebrows. Weights 400 / 500 / 600 / 700.
2. **IBM Plex Mono** — used for code, CLI output, YAML snippets, and the entire `card-terminal-panel` surface.

Inter loads via `@font-face` in `tokens.css` (variable, jsdelivr CDN). IBM Plex Mono is referenced by name; pre-installed on most dev machines and degrades gracefully through `ui-monospace`.

### Hierarchy

| Token | Size | Weight | Line Height | Letter Spacing | Use |
|---|---|---|---|---|---|
| `{typography.display-xl}` | 56px | 500 | 56px | 0 | Hero headline ("Tickets become PRs.") |
| `{typography.display-lg}` | 48px | 500 | 48px | 0 | Sub-hero, section displays |
| `{typography.display-md}` | 32px | 500 | 36px | 1px | Section displays with positive tracking |
| `{typography.display-sub-lg}` | 48px | 500 | 49.92px | 0 | Inter-rendered sub-display |
| `{typography.display-sub-md}` | 32px | 400 | 40px | 0 | Inter sub-display |
| `{typography.display-sub-sm}` | 24px | 600 | 30px | -0.6px | Card titles, semibold |
| `{typography.display-xs}` | 20px | 700 | 25px | -0.5px | Inline display micro-headings |
| `{typography.body-lg}` | 20px | 400 | 30px | -0.2px | Lead paragraphs |
| `{typography.body-md}` | 18px | 400 | 27px | 0 | Default body |
| `{typography.body-md-strong}` | 18px | 600 | 27px | 0 | Bolded inline body |
| `{typography.body-sm}` | 16px | 400 | 24px | 0 | Secondary body, button-text bodies |
| `{typography.body-sm-strong}` | 16px | 600 | 24px | 0 | Bold caption |
| `{typography.caption}` | 14px | 400 | 21px | 0 | Fine print, footer links |
| `{typography.eyebrow-uppercase}` | 14px | 500 | 14px | 1px | UPPERCASE eyebrow above section headlines |
| `{typography.button-md}` | 18px | 600 | 27px | 0 | Primary button label |
| `{typography.button-sm}` | 14.4px | 700 | 14.4px | 0.144px | Small button label |
| `{typography.mono-code}` | 14px | 400 | 21px | 0 | CLI output, code blocks, terminal panels |
| `{typography.mono-caption}` | 12px | 400 | 18px | 0 | Mono fine print, log line numbers |

### Principles
- **Inter 500 for hero, Inter for everything else.** One face, role-separated by weight.
- **IBM Plex Mono for every code / CLI / YAML surface.** Never substitute with Inter at body sizes — the monospace IS the developer-tool signal.
- **Positive tracking on the eyebrow** — 1 px at 14 px is the brand's signature label style.
- **Sentence-case headlines.** The brand never uppercases display sizes. Headlines read as a sentence ending in a period.

## Layout

### Spacing System
- **Base unit**: 4 px.
- **Tokens**: `{spacing.xxs}` 2 px · `{spacing.xs}` 4 px · `{spacing.sm}` 8 px · `{spacing.md}` 12 px · `{spacing.lg}` 16 px · `{spacing.xl}` 24 px · `{spacing.2xl}` 32 px · `{spacing.3xl}` 48 px · `{spacing.4xl}` 64 px.
- **Section padding**: bands use `{spacing.4xl}` 64 px top/bottom.
- **Card interior**: cards at `{spacing.xl}` 24 px; pricing tiers and feature cards at `{spacing.xl}` 24 px too — the 32 px tier from the source system is dropped, 24 px is enough.

### Grid & Container
- Marketing container ~1280 px wide; centred with gutters.
- Hero: centred at desktop, headline + button row above, `workspace-mockup-card` floats below.
- Pipeline taxonomy grid: 3-up at desktop, 2-up tablet, 1-up mobile.

### Responsive Strategy

#### Breakpoints

| Name | Width | Key Changes |
|---|---|---|
| Mobile | < 768px | Hero stacks; grids 1-up; hamburger nav. Hero display drops 56 → 36px. |
| Tablet | 768–1023px | 2-up grids. Hero display 48px. |
| Desktop | ≥ 1024px | Full grids; centred hero. |

#### Touch Targets
Buttons render ~48 px tall (12 vertical padding + 27 line). WCAG AAA met.

#### Image Behavior
The brand uses **animated SVG diagrams** (most prominently the lifecycle diagram) and **embedded mock-up cards** (CLI output, dashboards, k8s pod specs) rather than photography. Photography appears only in customer logo strips, if ever.

## Elevation & Depth

| Level | Treatment | Use |
|---|---|---|
| Level 0 — Flat | No shadow, no border. | Default for hero, content bands. |
| Level 1 — Hairline | 1 px solid `{colors.mute}` border. | Pricing-tier card chrome, outline buttons, terminal-panel rim. |
| Level 2 — Soft Card | `{colors.canvas-soft}` cream fill against `{colors.canvas}` page. | Default content cards — surface contrast carries elevation. |
| Level 3 — Mockup Drop | `rgba(32, 21, 21, 0.18) 0px 24px 48px -8px` deep diffuse shadow. | `workspace-mockup-card` only — the hero diagram floating over the cream band. |

## Shapes

### Border Radius Scale

| Token | Value | Use |
|---|---|---|
| `{rounded.none}` | 0px | Full-bleed bands |
| `{rounded.sm}` | 6px | Inline pills, form inputs |
| `{rounded.md}` | 12px | Buttons, cards, terminal panels — the brand's canonical radius |
| `{rounded.pill}` | 9999px | Status pills, badges |
| `{rounded.full}` | 9999px | Circular icon containers |

The brand's geometry is sober-editorial — `{rounded.md}` 12 px buttons distinguish it from pill-everywhere brands.

## Components

### Buttons

**`button-primary`** — the green CTA, the dominant action.
- Background `{colors.primary}`, text `{colors.on-primary}` (warm white), label `{typography.button-md}`, padding `{spacing.md} {spacing.xl}`, shape `{rounded.md}`.

**`button-secondary`** — the dark coffee-ink CTA.
- Background `{colors.ink}`, text `{colors.on-primary}`, same typography / padding / shape.

**`button-tertiary`** — the outline CTA.
- Background `{colors.canvas}`, text `{colors.ink}`, 1 px solid `{colors.ink}` border, same typography / padding / shape.

**`button-text`** — text-only CTA used inside cards and nav.
- Background `{colors.canvas}`, text `{colors.ink}`, body in `{typography.button-sm}`, padding `{spacing.sm} {spacing.lg}`, shape `{rounded.md}`.

### Cards & Containers

**`card-content`** — the default cream content card. Background `{colors.canvas-soft}`, padding `{spacing.xl}`, shape `{rounded.md}`.

**`card-feature-cream`** — feature card chrome, same as `card-content` with stronger heading hierarchy inside.

**`card-feature-dark`** — polarity-flipped dark coffee card. Background `{colors.ink}`, text `{colors.on-primary}`.

**`card-terminal-panel`** — *Agent Smith specific*. Dark-ink monospace surface for CLI output, YAML snippets, code samples. Background `{colors.terminal-bg}`, text `{colors.terminal-text}`, typography `{typography.mono-code}`, padding `{spacing.xl}`, shape `{rounded.md}`. Carries an accent (`{colors.terminal-accent}` Smith green) for prompt glyphs and an emphasis tone, and a mute (`{colors.terminal-mute}`) for comments. No syntax highlighting beyond these two accents — by design.

**`pricing-card`** — the default pricing tier card. Background `{colors.canvas}`, 1 px solid `{colors.ink}` border, padding `{spacing.xl}`, shape `{rounded.md}`.

**`pricing-card-featured`** — the polarity-flipped featured pricing tier. Background `{colors.ink}`, text `{colors.on-primary}`.

**`workspace-mockup-card`** — the hero diagram container. Background `{colors.canvas}`, 1 px `{colors.mute}` border, shape `{rounded.md}`, Level 3 shadow. Wraps the lifecycle SVG on the landing.

### Pipeline Taxonomy Cards

Deterministic tint mapping — the same card chrome means the same pipeline across every surface (landing, docs, future Sentinel UI).

| Pipeline | Component | Polarity |
|---|---|---|
| `fix-bug` | `pipeline-fix-bug` | Cream |
| `add-feature` | `pipeline-add-feature` | Cream + outline |
| `security-scan` | `pipeline-security-scan` | Dark |
| `api-security-scan` | `pipeline-api-security-scan` | Dark |
| `legal-analysis` | `pipeline-legal-analysis` | Cream |
| `mad-discussion` | `pipeline-mad-discussion` | Cream |
| `init-project` | `pipeline-init-project` | Cream |
| `autonomous` | `pipeline-autonomous` | Dark |
| `skill-manager` | `pipeline-skill-manager` | Dark |

Rule of thumb: code-changing pipelines (fix / feature / legal / mad / init) are cream — they're the friendly default. Audit / admin pipelines (security / api-security / autonomous / skill-manager) are dark — they signal severity or operator-mode.

### Inputs & Forms

**`text-input`** — Background `{colors.canvas}`, text `{colors.ink}`, 1 px solid `{colors.ink}` border, body in `{typography.body-md}`, padding `{spacing.md} {spacing.lg}`, shape `{rounded.sm}`.

### Navigation

**`nav-bar`** — Background `{colors.canvas}`, text `{colors.ink}`, padding `{spacing.md} {spacing.xl}`. Bottom 1 px `{colors.mute}` hairline.

**`nav-link`** — Text `{colors.ink}`, set in `{typography.body-sm}`.

**`footer-region`** — Background `{colors.ink}`, text `{colors.canvas-soft}`, padding `{spacing.3xl} {spacing.xl}`. Body in `{typography.body-sm}`. Three-column link grid; fine-print row at the bottom in `{typography.caption}`.

### Signature Components

**`hero-band`** — cream hero band. Background `{colors.canvas}`, text `{colors.ink}`, padding `{spacing.4xl} {spacing.xl}`. Headline in `{typography.display-xl}` (Inter 56 px / 500). Eyebrow `eyebrow-uppercase` above the headline.

**`hero-band-dark`** — polarity-flipped dark coffee hero. Background `{colors.ink}`, text `{colors.on-primary}`, same scale.

**`content-band-cream`** — cream content band that follows hero. Background `{colors.canvas-soft}`, padding `{spacing.4xl} {spacing.xl}`. Section headline in `{typography.display-lg}`.

**`content-band-light`** — white content band. Background `{colors.canvas}`, same padding / scale.

**`eyebrow-uppercase`** — small UPPERCASE Inter eyebrow above section headlines. Text `{colors.ink}`, set in `{typography.eyebrow-uppercase}` (14 px / 500 / 1 px tracking).

**`badge-pill`** — inline pill for metadata / tag. Background `{colors.canvas-soft}`, text `{colors.ink}`, body in `{typography.body-sm}`, padding `{spacing.xs} {spacing.md}`, shape `{rounded.pill}`.

**`badge-pill-green`** — emphasis pill (e.g. "new", "popular"). Background `{colors.primary}`, text `{colors.on-primary}`, body in `{typography.body-sm-strong}`.

## Do's and Don'ts

### Do
- Reserve `{colors.primary}` Smith green for every primary CTA. One filled green button per band.
- Keep canvas WARM — `{colors.canvas}` `#fffefb` cream, not pure white. The temperature is the brand voice.
- Set hero headlines in `{typography.display-xl}` Inter weight 500. Sentence-case, no uppercase.
- Use `{rounded.md}` 12 px for buttons + cards. The middle radius is the brand's signature.
- Pair Smith green CTA with ink-dark text on cream backgrounds — the three-token rhythm is the brand's whole conversion story.
- Wrap every code / CLI / YAML surface in `card-terminal-panel`. Use `mono-code` typography exclusively inside.
- Use the deterministic pipeline-card tint mapping. Same chrome means same pipeline across every surface.
- Apply the polarity flip (`*-dark` variants) for the severity / operator-mode pipelines (security, autonomous, skill-manager).

### Don't
- Don't replace cream canvas with pure white. The warmth IS the brand.
- Don't use pure black ink. The coffee-warmth in `#201515` carries through every text color.
- Don't render CTAs as pills. The brand's button is 12 px rounded rectangle.
- Don't introduce a second chromatic accent. Smith-green + cream + coffee is the entire palette.
- Don't substitute Inter with Helvetica or generic system-ui for displays. Inter weight 500 has the editorial proportions the brand expects.
- Don't mix `card-terminal-panel` typography with Inter inside the same panel. Monospace stays mono, end to end.
- Don't use the Smith-green as a body-text color — it's a CTA / accent only, never body type.

## Responsive Behavior

Already covered under Layout > Responsive Strategy. One emphasis: the lifecycle SVG inside `workspace-mockup-card` simplifies on mobile to a vertical stack (ticket -> sandbox -> PR -> resolved), not the horizontal fan-out — the multi-sandbox-row composition only renders at desktop+.

## Swapping the Design System

This is the operational contract.

**Token NAMES are stable. Token VALUES are free to change.**

To swap to a different design tomorrow:

1. Replace the YAML frontmatter of this file. Keep token names (`colors.primary`, `typography.display-xl`, `rounded.md`, `spacing.xl`, `components.button-primary`, etc.) identical.
2. Run `node scripts/build-tokens.mjs` from the repo root. The script regenerates `website/src/static/tokens.css` and `docs/stylesheets/tokens.css`.
3. Both sites pick up the new values automatically; no markup changes needed.

If you need a NEW token (e.g. an accent color the previous system didn't have), add it to the frontmatter, regenerate, and use it via `var(--your-token)` in the sites that need it. Keep deprecated tokens as aliases for one minor version before removing them — that's the swap-back path.

The Components section above documents the *role* of each component using token references. A swap that changes only colors / typography stays compatible with this whole document; a swap that changes *components* (new component family, renamed roles) requires updating the prose too.

This file is the only Agent-Smith design contract. `website/` and `docs/` consume it via `tokens.css`. The CI test for the design system is: `node scripts/build-tokens.mjs` exits 0, both sites build green.
