# Phase 4 - Schritt 1: IntentParser

## Ziel
User Input wie `"fix #123 in payslip"` in strukturierten `ParsedIntent` umwandeln.
Projekt: `AgentSmith.Application/Services/`

---

## RegexIntentParser

```
Datei: src/AgentSmith.Application/Services/RegexIntentParser.cs
```

Implementiert `IIntentParser` aus Contracts.

**Unterstützte Patterns:**
```
"fix #123 in payslip"      → #123, payslip
"#34237 payslip"            → #34237, payslip
"payslip #123"              → #123, payslip
"fix 123 in payslip"        → 123, payslip
"resolve ticket #42 in api" → #42, api
```

**Regex-Strategie:**
1. TicketId extrahieren: `#?(\d+)` - Zahl mit optionalem `#`
2. ProjectName extrahieren: Bekannte Noise-Wörter entfernen (`fix`, `resolve`, `in`, `ticket`, etc.)
   → übrig gebliebenes Wort = ProjectName

**Alternativ-Ansatz (einfacher):**
- Regex 1: `#?(\d+)\s+(?:in\s+)?(\w+)` → Ticket zuerst
- Regex 2: `(\w+)\s+#?(\d+)` → Project zuerst
- Beide versuchen, erster Match gewinnt

**Validierung:**
- Kein Match → `ConfigurationException("Could not parse intent from input: ...")`
- TicketId muss numerisch sein
- ProjectName muss nicht-leer sein

**Constructor:**
- `ILogger<RegexIntentParser> logger`

---

## Erweiterbarkeit

Später kann ein `ClaudeIntentParser` als Alternative registriert werden:
```csharp
// DI: Austausch durch Config oder Feature Flag
services.AddTransient<IIntentParser, RegexIntentParser>();
// oder:
services.AddTransient<IIntentParser, ClaudeIntentParser>();
```

---

## Tests

**RegexIntentParserTests:**
- `ParseAsync_FixHashInProject_ReturnsCorrectIntent`
- `ParseAsync_ProjectFirst_ReturnsCorrectIntent`
- `ParseAsync_NoHash_ReturnsCorrectIntent`
- `ParseAsync_InvalidInput_ThrowsConfigurationException`
- `ParseAsync_OnlyNumber_ThrowsConfigurationException`
