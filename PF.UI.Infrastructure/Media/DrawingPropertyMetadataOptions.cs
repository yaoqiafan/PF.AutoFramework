using System;

namespace PF.UI.Shared.Media;

[Flags]
public enum DrawingPropertyMetadataOptions
{
    AffectsMeasure = 1,
    AffectsRender = 0x10,
    None = 0
}
