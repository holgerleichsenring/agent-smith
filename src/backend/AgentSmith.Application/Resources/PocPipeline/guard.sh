set -e
[ "$POC_TRIVY_IGNORE" != none ] || POC_TRIVY_IGNORE=""
[ "$POC_SECRET_RULES" != none ] || POC_SECRET_RULES=""
poc_check() {
  if [ "$(printf '%s' "$2" | wc -l)" -ne 0 ] || ! printf '%s\n' "$2" | grep -Eqx -- "$3"; then
    echo "input $1 is refused: it must match $3" >&2
    exit 1
  fi
}
poc_check poc "$POC_NAME" '[a-z0-9]([-a-z0-9]*[a-z0-9])?'
poc_check folder "$POC_FOLDER" '@@ROOT_REGEX@@/[a-z0-9-]+/[a-z0-9-]+'
[ "${POC_FOLDER##*/}" = "$POC_NAME" ] || { echo "input folder does not end in the PoC's name" >&2; exit 1; }
poc_check sha "$POC_SHA" '[0-9a-f]{40}'
[ -z "$POC_TRIVY_IGNORE" ] || poc_check trivy_ignore "$POC_TRIVY_IGNORE" '[A-Z][A-Z0-9]*-[A-Za-z0-9._:-]{1,80}(,[A-Z][A-Z0-9]*-[A-Za-z0-9._:-]{1,80})*'
[ -z "$POC_SECRET_RULES" ] || poc_check secret_rules "$POC_SECRET_RULES" '[a-z0-9-]{1,80}(,[a-z0-9-]{1,80})*'
poc_check ignore_unfixed "$POC_IGNORE_UNFIXED" 'true|false'
