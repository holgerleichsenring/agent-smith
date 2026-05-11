# syntax=docker/dockerfile:1
# Generic toolchain image used by SandboxSpecBuilder when ProjectMap.PrimaryLanguage
# doesn't match one of the recognised language stacks (dotnet / node / python / go /
# rust) AND for the init-project window before AnalyzeCode populates ProjectMap.
#
# Two hard requirements drive the base-image choice:
#   - glibc: the sandbox-agent is a self-contained .NET 8 binary built against
#     glibc (carrier base is dotnet/runtime-deps:8.0-bookworm-slim). musl-libc
#     toolchains (alpine, chainguard, distroless-musl) crash with the misleading
#     "exec /shared/agent: no such file or directory" because the kernel can't
#     find the glibc dynamic linker referenced in the agent's ELF header.
#   - git: CheckoutSourceCommand runs `git clone` from inside the toolchain via
#     the agent's step protocol. Without git on PATH, every clone fails with
#     "no such file or directory" pointing at the git binary itself.
#
# debian:bookworm-slim is the smallest official glibc base; git is added via
# apt (~50 MB on top of the 80 MB base → ~130 MB total). Bitnami/git was the
# obvious off-the-shelf alternative but ships at 955 MB with a stack of
# unwanted runtime tooling. Custom image gives us the right size + zero
# external dependencies.
FROM debian:bookworm-slim
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        git \
        ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# No ENTRYPOINT / CMD: the sandbox toolchain is invoked by DockerSandboxFactory
# with Cmd=/shared/agent — the loader carrier injects the agent binary into
# the shared volume and the toolchain exec's it as PID 1.
