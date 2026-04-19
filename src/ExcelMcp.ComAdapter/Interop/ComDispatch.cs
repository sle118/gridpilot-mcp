using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ExcelMcp.ComAdapter.Interop;

[SupportedOSPlatform("windows")]
internal static class ComDispatch
{
    public static T GetProperty<T>(object target, string propertyName)
    {
        var value = target.GetType().InvokeMember(
            propertyName,
            BindingFlags.GetProperty,
            binder: null,
            target,
            args: null);

        return value is null ? default! : (T)value;
    }

    public static void SetProperty(object target, string propertyName, object? value)
    {
        target.GetType().InvokeMember(
            propertyName,
            BindingFlags.SetProperty,
            binder: null,
            target,
            args: [value]);
    }

    public static object? InvokeMethod(object target, string methodName, params object?[]? args)
    {
        return target.GetType().InvokeMember(
            methodName,
            BindingFlags.InvokeMethod,
            binder: null,
            target,
            args);
    }

    public static IEnumerable Enumerate(object target) => (IEnumerable)target;

    public static void ReleaseIfComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}
