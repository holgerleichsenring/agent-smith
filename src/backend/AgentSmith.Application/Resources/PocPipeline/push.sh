set -e
mkdir -p "@@OUT@@"
gunzip -c "@@IN@@" | docker load
ref="@@IMAGE@@/$POC_NAME:$POC_SHA"
docker tag poc-image "$ref"
@@LOGIN@@
docker push "$ref"
digest=$(docker inspect --format '{{range .RepoDigests}}{{println .}}{{end}}' "$ref" | grep "^@@IMAGE@@/$POC_NAME@" | head -n 1 | sed 's/.*@//')
printf '%s\n' "$digest" | grep -Eqx 'sha256:[0-9a-f]{64}' || { echo "the registry named no digest for $ref" >&2; exit 1; }
printf '%s' "$digest" > "@@OUT@@/digest.txt"
