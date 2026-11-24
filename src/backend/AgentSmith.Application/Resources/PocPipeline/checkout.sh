set -e
@@REPO_URL@@
git init -q src
cd src
git config core.sparseCheckout true
git config core.sparseCheckoutCone false
printf '/%s/\n' "$POC_FOLDER" > .git/info/sparse-checkout
git remote add origin "$POC_REPO_URL"
@@AUTH_ON@@
git fetch -q --depth 1 --filter=blob:none origin "$POC_SHA"
git checkout -q FETCH_HEAD
@@AUTH_OFF@@
git remote remove origin
