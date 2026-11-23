using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Semojum.Desktop;

public partial class MainWindow : Semojum.UI.Common.IEmbeddedScreenHost
{
    private bool embeddedScreenActive;

    Window Semojum.UI.Common.IEmbeddedScreenHost.Window => this;
    Task<T> Semojum.UI.Common.IEmbeddedScreenHost.ShowScreenAsync<T>(FrameworkElement screen, string title, Task<T> completion, FrameworkElement? caption)
        => ShowEmbeddedScreenAsync(screen, title, completion, caption);

    // Keep the native shell and its caption controls; replace only the editor content.
    internal async Task<T> ShowEmbeddedScreenAsync<T>(FrameworkElement screen, string title, Task<T> completion, FrameworkElement? caption = null)
    {
        if (embeddedScreenActive || Content is not Grid frame) throw new InvalidOperationException("다른 화면을 표시하는 중입니다.");
        var presenter = frame.Children.OfType<ContentPresenter>().Single();
        var captionBorder = (Border)frame.Children[0];
        var previousCaption = captionBorder.Child;
        var previousEnabled = presenter.IsEnabled;
        var previousState = WindowState;
        var previousTitle = Title;
        var previousBinding = BindingOperations.GetBindingBase(this, TitleProperty);
        var previousFocus = Keyboard.FocusedElement;
        var shortcuts = InputBindings.Cast<InputBinding>().ToArray();
        embeddedScreenActive = true;
        try
        {
            if (caption is not null) captionBorder.Child = caption;
            presenter.IsEnabled = false;
            InputBindings.Clear();
            SetCurrentValue(TitleProperty, title);
            WindowState = WindowState.Maximized;
            Grid.SetRow(screen, 1); Panel.SetZIndex(screen, 10); frame.Children.Add(screen);
            return await completion;
        }
        finally
        {
            frame.Children.Remove(screen);
            if (caption is not null) captionBorder.Child = previousCaption;
            presenter.IsEnabled = previousEnabled;
            foreach (var shortcut in shortcuts) InputBindings.Add(shortcut);
            if (previousBinding is not null) BindingOperations.SetBinding(this, TitleProperty, previousBinding);
            else SetCurrentValue(TitleProperty, previousTitle);
            if (IsVisible) WindowState = previousState;
            embeddedScreenActive = false;
            if (IsVisible && previousFocus is UIElement element && element.IsVisible && element.IsEnabled) Keyboard.Focus(element);
        }
    }
}
