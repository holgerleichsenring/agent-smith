/*
 * 2026-10-16-8648: the toolchain container's entry point. The agent is published twice — for
 * glibc and for musl — because an image picks its own C library and a glibc binary cannot exec
 * on musl (Alpine: hashicorp/terraform, *-alpine). This launcher is linked statically, so it
 * runs on either, looks for the musl loader, and execs the matching agent from its own
 * directory with the arguments it was given.
 *
 *   <dir>/agent      this launcher
 *   <dir>/agent-gnu  the glibc agent
 *   <dir>/musl/agent the musl agent, <dir>/musl/lib its libstdc++ and libgcc_s
 */
#include <errno.h>
#include <libgen.h>
#include <limits.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

#if defined(__x86_64__)
#define MUSL_LOADER "/lib/ld-musl-x86_64.so.1"
#elif defined(__aarch64__)
#define MUSL_LOADER "/lib/ld-musl-aarch64.so.1"
#else
#error "unsupported architecture"
#endif

int main(int argc, char **argv)
{
    (void)argc;
    char self[PATH_MAX];
    ssize_t n = readlink("/proc/self/exe", self, sizeof self - 1);
    if (n < 0) { perror("agent launcher: readlink /proc/self/exe"); return 127; }
    self[n] = '\0';
    char *dir = dirname(self);

    char target[PATH_MAX];
    if (access(MUSL_LOADER, F_OK) == 0) {
        char lib[PATH_MAX];
        snprintf(lib, sizeof lib, "%s/musl/lib", dir);
        const char *existing = getenv("LD_LIBRARY_PATH");
        char path[PATH_MAX * 2];
        if (existing && *existing) snprintf(path, sizeof path, "%s:%s", lib, existing);
        else snprintf(path, sizeof path, "%s", lib);
        setenv("LD_LIBRARY_PATH", path, 1);
        setenv("AGENTSMITH_MUSL_LIB", lib, 1);
        snprintf(target, sizeof target, "%s/musl/agent", dir);
    } else {
        snprintf(target, sizeof target, "%s/agent-gnu", dir);
    }

    argv[0] = target;
    execv(target, argv);
    fprintf(stderr, "agent launcher: exec %s failed: %s\n", target, strerror(errno));
    return 127;
}
