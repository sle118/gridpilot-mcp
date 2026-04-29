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

    public static bool TryGetProperty(object target, string propertyName, out object? value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property is null)
        {
            try
            {
                value = target.GetType().InvokeMember(
                    propertyName,
                    BindingFlags.GetProperty,
                    binder: null,
                    target,
                    args: null);

                return true;
            }
            catch (MissingMethodException)
            {
                value = null;
                return false;
            }
            catch (COMException)
            {
                value = null;
                return false;
            }
        }

        value = property.GetValue(target);
        return true;
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

    public static bool TryInvokeMethod(object target, string methodName, out object? value, params object?[]? args)
    {
        var methods = target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .ToArray();

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if ((args?.Length ?? 0) != parameters.Length)
            {
                continue;
            }

            try
            {
                value = method.Invoke(target, args);
                return true;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }

        try
        {
            value = target.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod,
                binder: null,
                target,
                args);

            return true;
        }
        catch (MissingMethodException)
        {
            value = null;
            return false;
        }
        catch (COMException)
        {
            value = null;
            return false;
        }
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
