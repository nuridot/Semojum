using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfMath.Parsers;
using WpfMath.Rendering;
using XamlMath.Fonts;

namespace Semojum.Desktop.Controls;

/// <summary>Renders formula text from a WPF font family, including bundled fonts.</summary>
public sealed class FormulaPreview : UserControl
{
    private readonly Image image = new() { Stretch = Stretch.None };
    public static readonly DependencyProperty FormulaProperty = DependencyProperty.Register(nameof(Formula), typeof(string), typeof(FormulaPreview), new PropertyMetadata("", Changed));
    public static readonly DependencyProperty TextFontFamilyProperty = DependencyProperty.Register(nameof(TextFontFamily), typeof(FontFamily), typeof(FormulaPreview), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(nameof(Scale), typeof(double), typeof(FormulaPreview), new PropertyMetadata(20d, Changed));
    public string Formula { get => (string)GetValue(FormulaProperty); set => SetValue(FormulaProperty, value); }
    public FontFamily TextFontFamily { get => (FontFamily?)GetValue(TextFontFamilyProperty) ?? (FontFamily)FindResource("InterfaceFont"); set => SetValue(TextFontFamilyProperty, value); }
    public double Scale { get => (double)GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }
    public bool HasError { get; private set; }
    internal DrawingImage? Drawing => image.Source as DrawingImage;

    public FormulaPreview() { Content = image; }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((FormulaPreview)sender).Render();
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ForegroundProperty) Render();
    }

    private void Render()
    {
        if (image is null) return;
        image.Source = null; HasError = false;
        if (string.IsNullOrWhiteSpace(Formula)) return;
        try
        {
            var scale = Math.Max(1, Scale);
            var environment = WpfTeXEnvironment.Create(scale: scale, foreground: Foreground);
            // WpfMath 2.1.0 exposes only a system-font-name factory. Its text font
            // has a public (double, FontFamily) constructor on an internal type.
            // Keep this compatibility bridge here, rather than installing fonts
            // globally or relying on GDI-private fonts appearing in DirectWrite.
            // The package is pinned; the bundled-font rendering check guards it.
            var font = (ITeXFont)Activator.CreateInstance(environment.TextFont.GetType(), scale, TextFontFamily)!;
            environment = environment with { TextFont = font };
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
                WpfTeXFormulaParser.Instance.Parse(Formula).RenderTo(context, environment, scale);
            image.Source = new DrawingImage(visual.Drawing);
        }
        catch (Exception)
        {
            // Missing glyphs and malformed formulas must clear the old drawing,
            // including exceptions outside WpfMath's TexException hierarchy.
            image.Source = null; HasError = true;
        }
    }
}
