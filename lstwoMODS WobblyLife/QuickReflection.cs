using System.Reflection;

namespace lstwoMODS_WobblyLife;

public class QuickReflection<T>
{
    public T instance;
    public BindingFlags flags;

    public QuickReflection(T instance, BindingFlags flags)
    {
        this.instance = instance;
        this.flags = flags;
    }

    public object GetField(string field)
    {
        return GetField(instance, flags, field);
    }

    public object GetMethod(string method, params object[] args)
    {
        return GetMethod(instance, method, flags, args);
    }

    public void SetField(string field, object value)
    {
        SetField(instance, flags, field, value);
    }


    public static object GetField(T instance, BindingFlags flags, string field)
    {
        var info = typeof(T).GetField(field, flags);
        return info.GetValue(instance);
    }

    public static object GetMethod(T instance, string method, BindingFlags flags = default, params object[] args)
    {
        var info = typeof(T).GetMethod(method, flags);
        return info.Invoke(instance, args);
    }

    public static void SetField(T instance, BindingFlags flags, string field, object value)
    {
        var info = typeof(T).GetField(field, flags);
        info.SetValue(instance, value);
    }
}