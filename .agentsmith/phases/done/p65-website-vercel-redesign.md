# Phase 65: Website Redesign — Vercel/Geist Style

## Goal

Redesign agent-smith.org to follow the Vercel design system aesthetic:
Geist fonts, shadow-as-border, monochrome palette, aggressive negative
letter-spacing, multi-layer shadow stacks. Keep all content and functionality
intact — this is a visual redesign, not a content rewrite.

## What Changes

### Fonts
- **DM Sans** → **Geist** (via CDN or self-hosted)
- **JetBrains Mono** → **Geist Mono**
- Aggressive negative letter-spacing at display sizes (-2.4px at 48px)
- Three weights only: 400 (body), 500 (UI), 600 (headings)
- OpenType `"liga"` globally

### Color Palette
- **Green accent (#00a854)** → monochrome with workflow-specific accents
- **Primary text**: `#171717` (not pure black)
- **Background**: `#ffffff`
- **Secondary text**: `#666666`
- **Borders**: shadow-as-border `rgba(0,0,0,0.08) 0px 0px 0px 1px`
- **Accent colors** (pipeline-specific, not global):
  - Coding: `#0a72ef` (Develop Blue)
  - Security: `#ff5b4f` (Ship Red)
  - Legal: `#de1d8d` (Preview Pink)

### Shadow System
- Remove all CSS `border` on cards/containers
- Replace with `box-shadow: 0px 0px 0px 1px rgba(0,0,0,0.08)`
- Card shadow stack: border + elevation + ambient + inner `#fafafa` ring
- No heavy shadows (max 0.1 opacity)

### Components
- **Buttons**: 6px radius, shadow-border for ghost, `#171717` for primary
- **Cards**: 8px radius, shadow stack, no CSS border
- **Badges**: 9999px pill, tinted backgrounds
- **Nav**: white sticky, shadow-border bottom, 14px weight 500 links
- **Terminal boxes**: keep dark theme but update to match Geist Mono

### Layout
- 8px base spacing unit
- Max content width ~1200px
- Gallery-level whitespace between sections (80-120px)
- Section separation via shadow-borders and spacing, not background colors

## What Stays
- Eleventy + Nunjucks templating
- All partials structure
- All data files (pipelines.json, skills.json, etc.)
- All JavaScript functionality (demo animation, tabs, GitHub stars)
- Content and copy
- Vercel deployment config
- Favicon (update color to #171717)

## Files to Modify
- `website/src/index.njk` — font links, minor class adjustments
- `website/src/static/styles.css` — complete redesign
- `website/src/static/favicon.svg` — color update
- `website/src/_includes/partials/*.njk` — minimal class adjustments if needed

## Definition of Done
- [ ] Geist + Geist Mono fonts loaded
- [ ] All CSS variables updated to Vercel palette
- [ ] Shadow-as-border replaces all CSS borders
- [ ] Typography uses negative letter-spacing at display sizes
- [ ] Cards use multi-layer shadow stacks
- [ ] Buttons follow Vercel patterns (6px radius, shadow-border ghost)
- [ ] Nav updated to Vercel style (sticky white, shadow-border)
- [ ] Terminal boxes use Geist Mono
- [ ] Responsive breakpoints maintained
- [ ] All animations still work
- [ ] Site builds successfully with `npm run build`
