using System;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace AuthFix;

public sealed class AndroidJavaObjectSafe : IDisposable
{
    public AndroidJavaObject Inner { get; }

    private static readonly MethodInfo s_Call;
    private static readonly MethodInfo s_CallReturn;
    private static readonly MethodInfo s_CallStatic;
    private static readonly MethodInfo s_CallStaticReturn;

    private static readonly Type[] s_paramTypes =
        [typeof(string), typeof(Il2CppReferenceArray<Il2CppSystem.Object>)];

    static AndroidJavaObjectSafe()
    {
        var all = typeof(AndroidJavaObject).GetMethods(
            BindingFlags.Public | BindingFlags.Instance);

        s_Call = FindVoid(all, "Call");
        s_CallReturn = FindNonVoid(all, "Call");
        s_CallStatic = FindVoid(all, "CallStatic");
        s_CallStaticReturn = FindNonVoid(all, "CallStatic");
    }

    private static MethodInfo FindVoid(MethodInfo[] methods, string name) =>
        methods.FirstOrDefault(m =>
            m.Name == name &&
            !m.IsGenericMethod &&
            m.ReturnType == typeof(void) &&
            m.GetParameters().Select(p => p.ParameterType).SequenceEqual(s_paramTypes));

    private static MethodInfo FindNonVoid(MethodInfo[] methods, string name) =>
        methods.FirstOrDefault(m =>
            m.Name == name &&
            !m.IsGenericMethod &&
            m.ReturnType != typeof(void) &&
            m.GetParameters().Select(p => p.ParameterType).SequenceEqual(s_paramTypes));

    // ── Constructors ───────────────────────────────────────────────────────────

    public AndroidJavaObjectSafe(string className,
        Il2CppReferenceArray<Il2CppSystem.Object> args = null)
    {
        Inner = new AndroidJavaObject(className, args ?? new Il2CppReferenceArray<Il2CppSystem.Object>(0L));
    }

    public AndroidJavaObjectSafe(AndroidJavaObject existing)
    {
        Inner = existing ?? throw new ArgumentNullException(nameof(existing));
    }

    public void Call(string method, Il2CppReferenceArray<Il2CppSystem.Object> args = null)
    {
        args ??= new Il2CppReferenceArray<Il2CppSystem.Object>(0L);
        s_Call.Invoke(Inner, [method, args]);
    }

    public Il2CppSystem.Object CallReturn(string method,
        Il2CppReferenceArray<Il2CppSystem.Object> args = null)
    {
        args ??= new Il2CppReferenceArray<Il2CppSystem.Object>(0L);
        return s_CallReturn.Invoke(Inner, [method, args]) as Il2CppSystem.Object;
    }

    public void CallStatic(string method,
        Il2CppReferenceArray<Il2CppSystem.Object> args = null)
    {
        args ??= new Il2CppReferenceArray<Il2CppSystem.Object>(0L);
        s_CallStatic.Invoke(Inner, [method, args]);
    }

    public Il2CppSystem.Object CallStaticReturn(string method,
        Il2CppReferenceArray<Il2CppSystem.Object> args = null)
    {
        args ??= new Il2CppReferenceArray<Il2CppSystem.Object>(0L);
        return s_CallStaticReturn.Invoke(Inner, [method, args]) as Il2CppSystem.Object;
    }

    // ── Pass-through

    public IntPtr GetRawObject() => Inner.GetRawObject();
    public IntPtr GetRawClass() => Inner.GetRawClass();
    public void Dispose() => Inner.Dispose();

    // ── Convenience

    public static Il2CppReferenceArray<Il2CppSystem.Object> Args(
        params Il2CppSystem.Object[] args) => new(args);
}