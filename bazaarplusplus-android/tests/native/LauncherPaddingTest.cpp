#include <hooking/allocator.h>
#include <logger.h>
#include <cassert>
#include <cstdio>
#include <cstring>
#include <dlfcn.h>

void log(LogLevel, const char *tag, const char *message) {
    std::fprintf(stderr, "%s: %s\n", tag, message);
}

int main(int argc, char **argv) {
    assert(argc == 3);
    void *handle = allocate_setup_injected(argv[1], argv[2], 1024 * 1024);
    assert(handle != nullptr);
    auto value = reinterpret_cast<int (*)()>(dlsym(handle, "padding_fixture_value"));
    assert(value && value() == 42);
    void *pool = allocate_injected(reinterpret_cast<void *>(value), nullptr, 8);
    assert(pool && reinterpret_cast<uintptr_t>(pool) == get_injected_pool_base());
    // AArch64: mov w0, #73; ret. Exercise the actual mapped executable pool.
    const uint32_t instructions[] = {0x52800920, 0xd65f03c0};
    std::memcpy(pool, instructions, sizeof(instructions));
    __builtin___clear_cache(static_cast<char *>(pool), static_cast<char *>(pool) + sizeof(instructions));
    assert(reinterpret_cast<int (*)()>(pool)() == 73);
    assert(allocate_injected(reinterpret_cast<void *>(value), nullptr, 1024 * 1024) == nullptr);
    assert(allocate_setup_injected("/nonexistent/bpp-padding-fixture.so", argv[2], 4096) == nullptr);
    assert(get_injected_pool_base() == 0);
    assert(allocate_injected(reinterpret_cast<void *>(value), nullptr, 8) == nullptr);
    std::puts("Native library padding, executable pool and failure reset passed");
}
