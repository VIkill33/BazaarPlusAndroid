#include <jni.h>
#include <cstring>
#include <cstdlib>
#include <dlfcn.h>
#include <cstdio>
#include <cstdint>

extern "C" int bpp_history_verify(const char*, char*, int);
extern "C" int bpp_history_write(const char*, char*, int);
extern "C" int bpp_history_read(const char*, char*, int);

namespace {
JavaVM* vm = nullptr;
jclass environmentClass = nullptr;
jmethodID verifyMethod = nullptr;
jclass gatewayClass = nullptr;
jmethodID writeMethod = nullptr;
jmethodID readMethod = nullptr;

bool copy(const char* value, char* output, int capacity) {
    const size_t size = std::strlen(value);
    if (size >= static_cast<size_t>(capacity)) return false;
    std::memcpy(output, value, size + 1);
    return true;
}
}

extern "C" JNIEXPORT jint JNICALL JNI_OnLoad(JavaVM* javaVm, void*) {
    JNIEnv* env = nullptr;
    if (javaVm->GetEnv(reinterpret_cast<void**>(&env), JNI_VERSION_1_6) != JNI_OK) return JNI_ERR;
    jclass local = env->FindClass("dev/allofus/fusioncore/history/HistoryEnvironment");
    if (!local) return JNI_ERR;
    environmentClass = static_cast<jclass>(env->NewGlobalRef(local));
    env->DeleteLocalRef(local);
    if (!environmentClass) return JNI_ERR;
    verifyMethod = env->GetStaticMethodID(environmentClass, "verify", "(Ljava/lang/String;)Ljava/lang/String;");
    if (!verifyMethod) {
        env->DeleteGlobalRef(environmentClass);
        environmentClass = nullptr;
        return JNI_ERR;
    }
    local = env->FindClass("dev/allofus/fusioncore/history/HistoryGateway");
    if (!local) return JNI_ERR;
    gatewayClass = static_cast<jclass>(env->NewGlobalRef(local));
    env->DeleteLocalRef(local);
    if (!gatewayClass) return JNI_ERR;
    writeMethod = env->GetStaticMethodID(gatewayClass, "write", "(Ljava/lang/String;)Ljava/lang/String;");
    if (!writeMethod) return JNI_ERR;
    readMethod = env->GetStaticMethodID(gatewayClass, "read", "(Ljava/lang/String;)Ljava/lang/String;");
    if (!readMethod) return JNI_ERR;
    vm = javaVm;
    Dl_info location{};
    if (dladdr(reinterpret_cast<void*>(&copy), &location) != 0 && location.dli_fname) {
        setenv("BPP_HISTORY_JNI_PATH", location.dli_fname, 1);
    }
    // CoreCLR can use a different Android linker namespace. Loading the same file again there
    // creates a second copy with no JVM. Publish only this process's initialized ABI entry.
    char entry[32];
    std::snprintf(entry, sizeof(entry), "%llx",
            static_cast<unsigned long long>(reinterpret_cast<uintptr_t>(&bpp_history_verify)));
    setenv("BPP_HISTORY_VERIFY_ADDRESS", entry, 1);
    std::snprintf(entry, sizeof(entry), "%llx",
            static_cast<unsigned long long>(reinterpret_cast<uintptr_t>(&bpp_history_write)));
    setenv("BPP_HISTORY_WRITE_ADDRESS", entry, 1);
    std::snprintf(entry, sizeof(entry), "%llx",
            static_cast<unsigned long long>(reinterpret_cast<uintptr_t>(&bpp_history_read)));
    setenv("BPP_HISTORY_READ_ADDRESS", entry, 1);
    setenv("BPP_HISTORY_BRIDGE_ABI", "1", 1);
    return JNI_VERSION_1_6;
}

// Bounded UTF-8 exchange. No managed caller owns a JNI object, path or Java exception.
static int exchange(const char* request, char* output, int capacity, jclass owner, jmethodID method, size_t limit) {
    if (!output || capacity <= 0) return -1;
    output[0] = '\0';
    if (!vm || !request) { copy("History JNI host unavailable", output, capacity); return -1; }
    const size_t requestSize = strnlen(request, limit + 1);
    if (requestSize > limit) { copy("History request too large", output, capacity); return 2; }
    JNIEnv* env = nullptr;
    bool attached = false;
    jint status = vm->GetEnv(reinterpret_cast<void**>(&env), JNI_VERSION_1_6);
    if (status == JNI_EDETACHED) {
        if (vm->AttachCurrentThread(&env, nullptr) != JNI_OK) return -1;
        attached = true;
    } else if (status != JNI_OK) return -1;
    int result = -1;
    if (env->PushLocalFrame(16) == JNI_OK) {
        // Business JSON uses standard UTF-8, including supplementary characters and embedded escapes.
        jclass stringType = env->FindClass("java/lang/String");
        jmethodID constructor = stringType ? env->GetMethodID(stringType, "<init>", "([BLjava/lang/String;)V") : nullptr;
        jbyteArray input = constructor ? env->NewByteArray(static_cast<jsize>(requestSize)) : nullptr;
        if (input) env->SetByteArrayRegion(input, 0, static_cast<jsize>(requestSize), reinterpret_cast<const jbyte*>(request));
        jstring charset = input && !env->ExceptionCheck() ? env->NewStringUTF("UTF-8") : nullptr;
        jstring argument = charset ? static_cast<jstring>(env->NewObject(stringType, constructor, input, charset)) : nullptr;
        jstring response = nullptr;
        if (argument && !env->ExceptionCheck()) response = static_cast<jstring>(env->CallStaticObjectMethod(owner, method, argument));
        jthrowable failure = env->ExceptionOccurred();
        if (failure) {
            env->ExceptionClear();
            jclass type = env->GetObjectClass(failure);
            jmethodID toString = type ? env->GetMethodID(type, "toString", "()Ljava/lang/String;") : nullptr;
            response = toString ? static_cast<jstring>(env->CallObjectMethod(failure, toString)) : nullptr;
        }
        if (env->ExceptionCheck()) { env->ExceptionClear(); response = nullptr; }
        if (response) {
            jclass stringClass = env->GetObjectClass(response);
            jmethodID getBytes = stringClass ? env->GetMethodID(stringClass, "getBytes", "(Ljava/lang/String;)[B") : nullptr;
            jstring encoding = getBytes && !env->ExceptionCheck() ? env->NewStringUTF("UTF-8") : nullptr;
            auto bytes = encoding && !env->ExceptionCheck()
                    ? static_cast<jbyteArray>(env->CallObjectMethod(response, getBytes, encoding)) : nullptr;
            if (bytes && !env->ExceptionCheck()) {
                jsize size = env->GetArrayLength(bytes);
                if (size < capacity) {
                    env->GetByteArrayRegion(bytes, 0, size, reinterpret_cast<jbyte*>(output));
                    output[size] = '\0';
                    result = failure ? 1 : 0;
                } else { result = 2; }
            }
        }
        env->PopLocalFrame(nullptr);
    }
    if (env->ExceptionCheck()) {
        env->ExceptionClear();
        output[0] = '\0';
        result = -1;
    }
    if (attached) vm->DetachCurrentThread();
    return result;
}

extern "C" __attribute__((visibility("default")))
int bpp_history_verify(const char* request, char* output, int capacity) {
    return exchange(request, output, capacity, environmentClass, verifyMethod, 128);
}
extern "C" __attribute__((visibility("default")))
int bpp_history_write(const char* request, char* output, int capacity) {
    return exchange(request, output, capacity, gatewayClass, writeMethod, 262144);
}
extern "C" __attribute__((visibility("default")))
int bpp_history_read(const char* request, char* output, int capacity) {
    return exchange(request, output, capacity, gatewayClass, readMethod, 8192);
}
