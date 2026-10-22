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
        return Unbox<T>(result, getter);
    }

    internal static unsafe T? Invoke<T, TFirst, TSecond>(Il2CppObjectBase instance, string methodName,
        TFirst first, TSecond second)
        where T : unmanaged where TFirst : unmanaged where TSecond : unmanaged
    {
        var method = IL2CPP.il2cpp_class_get_method_from_name(
            IL2CPP.il2cpp_object_get_class(instance.Pointer), methodName, 2);
        if (method == IntPtr.Zero) throw new MissingMethodException(methodName);
        void** arguments = stackalloc void*[2];
        arguments[0] = &first;
        arguments[1] = &second;
        var exception = IntPtr.Zero;
        var result = IL2CPP.il2cpp_runtime_invoke(method, instance.Pointer, arguments, ref exception);
        Il2CppException.RaiseExceptionIfNecessary(exception);
        return Unbox<T>(result, methodName);
    }

    internal static unsafe T? Invoke<T, TArgument>(Il2CppObjectBase instance, string methodName,
        TArgument argument) where T : unmanaged where TArgument : unmanaged
    {
        var method = IL2CPP.il2cpp_class_get_method_from_name(
            IL2CPP.il2cpp_object_get_class(instance.Pointer), methodName, 1);
        if (method == IntPtr.Zero) throw new MissingMethodException(methodName);
        void** arguments = stackalloc void*[1];
        arguments[0] = &argument;
        var exception = IntPtr.Zero;
        var result = IL2CPP.il2cpp_runtime_invoke(method, instance.Pointer, arguments, ref exception);
        Il2CppException.RaiseExceptionIfNecessary(exception);
        return Unbox<T>(result, methodName);
    }

    private static unsafe T? Unbox<T>(IntPtr result, string methodName) where T : unmanaged
    {
        if (result == IntPtr.Zero) return null;
        var resultClass = IL2CPP.il2cpp_object_get_class(result);
        if (resultClass == Il2CppClassPointerStore<T>.NativeClassPtr)
            return *(T*)IL2CPP.il2cpp_object_unbox(result);
        if (resultClass == Il2CppClassPointerStore<Il2CppSystem.Nullable<T>>.NativeClassPtr)
        {
            var nullable = new Il2CppSystem.Nullable<T>(result);
            return nullable.HasValue ? nullable.Value : null;
        }
        throw new InvalidOperationException("Unexpected native nullable result type for " + methodName);
    }

    internal static Il2CppSystem.Nullable<T> Box<T>(T? value) where T : unmanaged =>
        value.HasValue ? new Il2CppSystem.Nullable<T>(value.Value) : new Il2CppSystem.Nullable<T>();
}
