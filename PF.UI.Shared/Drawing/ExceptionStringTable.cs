using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace PF.UI.Shared.Drawing;

[CompilerGenerated]
[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
[DebuggerNonUserCode]
public class ExceptionStringTable
{
    private static ResourceManager ResourceMan;

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static CultureInfo Culture { get; set; }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static ResourceManager ResourceManager
    {
        get
        {
            if (ReferenceEquals(ResourceMan, null))
            {
                var manager = new ResourceManager("Microsoft.Expression.Drawing.ExceptionStringTable",
                    typeof(ExceptionStringTable).Assembly);
                ResourceMan = manager;
            }

            return ResourceMan;
        }
    }

    public static string TypeNotSupported =>
        ResourceManager.GetString("TypeNotSupported", Culture);
}
