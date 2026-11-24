package trivy

import rego.v1

default ignore := false

# A vulnerability gates only when the highest CVSS v3 score a source gives it is 9.0 or more, the CVSS v3.1
# Critical band; an unscored one is a note. Trivy's own severity label decides nothing.
v3_scores contains score if {
  some source
  score := input.CVSS[source].V3Score
  score > 0
}

ignore if {
  input.VulnerabilityID
  count(v3_scores) == 0
}

ignore if {
  input.VulnerabilityID
  max(v3_scores) < 9.0
}

# A secret gates only when Trivy rates it HIGH or CRITICAL and its rule is one the build passed in secret_rules.
ignore if {
  input.RuleID
  not input.Severity in {"HIGH", "CRITICAL"}
}

ignore if {
  input.RuleID
  not input.RuleID in blocking_secret_rules
}

# The scan job appends blocking_secret_rules from the guard-checked secret_rules input.
