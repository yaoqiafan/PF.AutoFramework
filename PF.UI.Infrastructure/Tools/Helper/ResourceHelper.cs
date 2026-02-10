using PF.UI.Infrastructure.Data;
using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace PF.UI.Infrastructure.Tools;

/// <summary>
///     资源帮助类
/// </summary>
public class ResourceHelper
{
    private static ResourceDictionary _theme;

    /// <summary>
    ///     获取资源
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static T GetResource<T>(string key)
    {
        if (Application.Current.TryFindResource(key) is T resource)
        {
            return resource;
        }

        return default;
    }

    public static T GetResourceInternal<T>(string key)
    {
        if (GetTheme()[key] is T resource)
        {
            return resource;
        }

        return default;
    }

    /// <summary>
    ///     获取皮肤
    /// </summary>
    public static ResourceDictionary GetSkin(Assembly assembly, string themePath, SkinType skin)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/{assembly.GetName().Name};component/{themePath}/Skin{skin}.xaml");
            return new ResourceDictionary
            {
                Source = uri
            };
        }
        catch
        {
            return new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/{assembly.GetName().Name};component/{themePath}/Skin{SkinType.Default}.xaml")
            };
        }
    }

    /// <summary>
    ///     get HandyControl skin
    /// </summary>
    /// <param name="skin"></param>
    /// <returns></returns>
    public static ResourceDictionary GetSkin(SkinType skin) => new()
    {
        Source = new Uri($"pack://application:,,,/PF.Resources;component/Colors/{skin}.xaml")
    };

    /// <summary>
    ///     get HandyControl theme
    /// </summary>
    public static ResourceDictionary GetTheme() => _theme ??= GetStandaloneTheme();

    public static ResourceDictionary GetStandaloneTheme()
    {
        return new()
        {
            Source = new Uri("pack://application:,,,/PF.Resources;component/Themes/Default.xaml")
        };
    }



    public static void OpenSplashScreen(string path = "Images/Loading_Default.jpg")
    {
        try
        {
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SplashScreen 错误: {ex.Message}");
        }
    }
}
