using System;
using System.ComponentModel;

namespace PF.UI.Infrastructure.Extensions;

public static class StringExtension
{
    public static T Value<T>(this string input)
    {
        try
        {
            return (T) TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(input);
        }
        catch
        {
            return default;
        }
    }

    public static object Value(this string input, Type type)
    {
        try
        {
            return TypeDescriptor.GetConverter(type).ConvertFromString(input);
        }
        catch
        {
            return null;
        }
    }
}
