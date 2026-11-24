set -e
[ "$POC_TRIVY_IGNORE" != none ] || POC_TRIVY_IGNORE=""
[ "$POC_SECRET_RULES" != none ] || POC_SECRET_RULES=""
mkdir -p "@@OUT@@"
gunzip -c "@@IN@@" > image.tar
cat > poc-report.tpl <<'POC_TPL'
@@REPORT_TPL@@
POC_TPL
cat > poc-gate.rego <<'POC_REGO'
@@GATE_REGO@@
POC_REGO
rules=$(printf '%s' "$POC_SECRET_RULES" | sed 's/[^,][^,]*/"&"/g')
if [ -n "$rules" ]; then echo "blocking_secret_rules := {$rules}" >> poc-gate.rego; else echo "blocking_secret_rules := set()" >> poc-gate.rego; fi
printf '%s' "$POC_TRIVY_IGNORE" | tr ',' '\n' > .trivyignore
unfixed=""
if [ "$POC_IGNORE_UNFIXED" = true ]; then unfixed="--ignore-unfixed"; fi
@@TRIVY_FN@@
trivy image --cache-dir .trivy-cache --input image.tar --scanners vuln,secret --quiet --format template --template "@poc-report.tpl" --output "@@OUT@@/report.json"
set +e
trivy image --cache-dir .trivy-cache --skip-db-update --input image.tar --scanners vuln,secret --quiet --exit-code 1 --ignore-policy poc-gate.rego --ignorefile .trivyignore $unfixed
gate=$?
set -e
echo "$gate" > "@@OUT@@/gate.txt"
exit "$gate"
