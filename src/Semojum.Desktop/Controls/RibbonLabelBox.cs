using System.Windows;
using System.Windows.Controls;

namespace Semojum.Desktop.Controls;

/// <summary>Figma's auto-width captions reserve the next whole DIP after measuring the glyphs.</summary>
public sealed class RibbonLabelBox : Decorator
{
    public RibbonLabelBox() => UseLayoutRounding = false;

    protected override Size MeasureOverride(Size constraint)
    {
        var desired = base.MeasureOverride(constraint);
        return new Size(Math.Ceiling(desired.Width), desired.Height);
    }
}
