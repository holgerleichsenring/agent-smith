set -e
docker build --pull -t poc-image "@@CONTEXT@@"
mkdir -p out
docker save poc-image > out/image.tar
gzip out/image.tar
