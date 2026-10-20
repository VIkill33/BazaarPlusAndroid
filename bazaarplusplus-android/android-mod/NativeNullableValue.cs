using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace BazaarPlusPlus.Android;

internal static class NativeNullableValue
{
    internal static unsafe T? Read<T>(Il2CppObjectBase instance, string getter) where T : unmanaged
    {
        var method = IL2CPP.il2cpp_class_get_method_from_name(IL2CPP.il2cpp_object_get_class(instance.Pointer), getter, 0);
        if (method == IntPtr.Zero) throw new MissingMethodException(getter);
        var target = instance is Il2CppSystem.ValueType ? IL2CPP.il2cpp_object_unbox(instance.Pointer) : instance.Pointer;
        var exception = IntPtr.Zero;
        var result = IL2CPP.il2cpp_runtime_invoke(method, target, null, ref exception);
        Il2CppException.RaiseExceptionIfNecessary(exception);
        if (result == IntPtr.Zero) return null;
        var resultClass = IL2CPP.il2cpp_object_get_class(result);
        if (resultClass == Il2CppClassPointerStore<T>.NativeClassPtr)
            return *(T*)IL2CPP.il2cpp_object_unbox(result);
        if (resultClass == Il2CppClassPointerStore<Il2CppSystem.Nullable<T>>.NativeClassPtr)
        {
            var nullable = new Il2CppSystem.Nullable<T>(result);
            return nullable.HasValue ? nullable.Value : null;
        }
        throw new InvalidOperationException("Unexpected native nullable result type for " + getter);
    }

    internal static Il2CppSystem.Nullable<T> Box<T>(T? value) where T : unmanaged =>
        value.HasValue ? new Il2CppSystem.Nullable<T>(value.Value) : new Il2CppSystem.Nullable<T>();
}
