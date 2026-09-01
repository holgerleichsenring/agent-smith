---
name: feedback_code_style
description: "User expects responsibility-modeled architecture, not code-moving refactors. Reference project shows the standard."
metadata:
  type: feedback
status: proposed
---
User builds code with 20-60 line services, one responsibility per class, interface for every
injectable, explicit DI. Reference: the operator's internal .NET background-worker service.

**Why:** "Es fehlt an klarer Abgrenzung, Aufteilung logischer Zusammenhänge und Transparenz."
My initial refactoring was "code moving" — extracting methods into files without modeling
responsibilities. User called this out.

**How to apply:**
- When splitting: ask "what are the responsibilities?" not "which methods to move"
- Every extraction gets: own type + interface + DI registration
- Composition over inheritance — always. No fat base classes.
- Static only for Map()/extensions. Builders/parsers/formatters = instance + Transient DI.
- No *Helper/*Utils/*Manager class names — name the responsibility.
- Base classes max 30 lines, contain only template method skeleton.
- User's coding principles file (.agentsmith/contexts/<name>/principles.md) was rewritten to reflect this.
