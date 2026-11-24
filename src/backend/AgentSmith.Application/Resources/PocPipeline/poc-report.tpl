[
{{- $first := true -}}
{{- range . -}}
{{- $target := .Target -}}
{{- range .Vulnerabilities -}}
{{- if $first }}{{ $first = false }}{{ else }},{{ end }}
{"kind":"vulnerability","id":{{ .VulnerabilityID | toJson }},"package":{{ .PkgName | toJson }},"installed":{{ .InstalledVersion | toJson }},"fixed":{{ .FixedVersion | toJson }},"severity":{{ .Severity | toJson }},"scores":[{{ $s := true }}{{ range $source, $cvss := .CVSS }}{{ if gt $cvss.V3Score 0.0 }}{{ if $s }}{{ $s = false }}{{ else }},{{ end }}{{ $cvss.V3Score }}{{ end }}{{ end }}],"title":{{ .Title | toJson }},"target":{{ $target | toJson }}}
{{- end -}}
{{- range .Secrets -}}
{{- if $first }}{{ $first = false }}{{ else }},{{ end }}
{"kind":"secret","id":{{ .RuleID | toJson }},"severity":{{ .Severity | toJson }},"title":{{ .Title | toJson }},"target":{{ $target | toJson }},"line":{{ .StartLine }}}
{{- end -}}
{{- end }}
]
