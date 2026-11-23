using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Desktop.Dialogs;
using SharpVectors.Converters;

namespace Semojum.Desktop.Services;

internal static class WindowsImageOrderChecks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var root = Directory.CreateTempSubdirectory("nuridot-image-order-");
        var parent = Directory.CreateDirectory(Path.Combine(root.FullName, "스캔본"));
        var folder = parent.CreateSubdirectory("2026_수능특강");
        var paths = Enumerable.Range(1, 5).Select(i => Path.Combine(folder.FullName, $"p{i:D3}.jpg")).ToArray();
        try
        {
            for (var i = 0; i < paths.Length; i++)
            {
                var pixels = new byte[2480 * 3508]; Array.Fill(pixels, (byte)(i == 1 ? 238 : 235 + i));
                var bitmap = BitmapSource.Create(2480, 3508, 96, 96, PixelFormats.Gray8, null, pixels, 2480);
                var encoder = new JpegBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(paths[i]); encoder.Save(output);
            }
            var extra = Path.Combine(parent.FullName, "가.png".Normalize(System.Text.NormalizationForm.FormD));
            File.Copy(paths[0], extra);
            var dialog = new ImageOrderDialog(paths.Take(4).ToArray());
            Exception? failure = null;
            dialog.ContentRendered += (_, _) => dialog.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    var browser = Descendants(dialog).OfType<ImageUploadBrowser>().Single();
                    var files = browser.Files;
                    ImageUploadBrowser.FileRow Row(string path) => files.Items.Cast<ImageUploadBrowser.FileRow>().Single(r => r.Path == path);
                    var submit = Descendants(dialog).OfType<Button>().Single(b => Equals(b.Content, "올리기"));
                    await Wait(() => files.Items.Count == 5 && browser.Preview.Source is not null);
                    var first = browser.Preview.Source;
                    files.SelectedItem = Row(paths[1]);
                    await Wait(() => browser.Preview.Source is not null && browser.Details.Text.Contains("p002.jpg"));
                    check(!ReferenceEquals(first, browser.Preview.Source) && browser.Details.Text.Contains("2480 × 3508") && browser.Details.Text.Contains("KB"),
                        "Selecting another image shows its decoded preview, original dimensions, and actual file size");
                    await WindowsSmoke.RenderAsync(dialog, Path.Combine(directory, "image-upload.png"));
                    check(dialog.ActualWidth == 900 && dialog.ActualHeight == 578, "Image upload uses the 900 by 578 Figma frame");
                    Bounds(browser.Frame, 25, 57, 850, 420, "three-column frame"); Bounds(files, 206, 66, 299, 402, "file list");
                    Bounds(browser.PreviewFrame, 570, 74, 240, 320, "preview"); Bounds(submit, 787, 529, 88, 32, "upload");
                    var svgs = Descendants(dialog).OfType<SvgViewbox>().Where(i => i.IsVisible).ToArray();
                    check(new[] { "form-close.svg", "image-folder.svg", "image-folder-selected.svg", "image-grip.svg" }.All(asset =>
                        svgs.Any(v => v.Source.ToString().EndsWith(asset) && v.Drawings is { Bounds.IsEmpty: false })), "All four source Figma icons render");
                    check(files.Items.Cast<ImageUploadBrowser.FileRow>().Select(r => r.Rank).SequenceEqual([1,2,3,4,0]) && browser.SelectedPaths.SequenceEqual(paths.Take(4)),
                        "Initial file order is preserved and an unchosen sibling is shown without a page number");
                    void Toggle(string path)
                    {
                        files.SelectedItem = Row(path);
                        files.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(files), 0, Key.Space) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                    }
                    foreach (var path in paths.Take(4)) Toggle(path);
                    check(!submit.IsEnabled && browser.SelectedPaths.Length == 0, "Removing the last included image disables upload");
                    foreach (var path in new[] { paths[2], paths[0], paths[1] }) Toggle(path);
                    check(browser.SelectedPaths.SequenceEqual([paths[2],paths[0],paths[1]]) && Row(paths[2]).Rank == 1, "Including images assigns order numbers in selection order");
                    files.SelectedItem = Row(paths[0]); files.UpdateLayout(); var target = (ListBoxItem)files.ItemContainerGenerator.ContainerFromItem(Row(paths[2]));
                    var data = new DataObject(); data.SetData("Semojum.ImageOrder.List", files); data.SetData("Semojum.ImageOrder.Path", paths[0]);
                    var ctor = typeof(DragEventArgs).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                        null, [typeof(IDataObject), typeof(DragDropKeyStates), typeof(DragDropEffects), typeof(DependencyObject), typeof(Point)], null)!;
                    var drop = (DragEventArgs)ctor.Invoke([data, DragDropKeyStates.LeftMouseButton, DragDropEffects.Move, target, new Point(1, 1)]);
                    drop.RoutedEvent = UIElement.DropEvent; target.RaiseEvent(drop);
                    check(browser.SelectedPaths.SequenceEqual([paths[0],paths[2],paths[1]]) && Equals(files.SelectedItem, Row(paths[0])), "Dragging renumbers the upload order and retains the active image");
                    foreach (var key in new[] { Key.Down, Key.Up })
                    {
                        var binding = files.InputBindings.Cast<KeyBinding>().Single(b => b.Key == key && b.Modifiers == ModifierKeys.Alt);
                        var command = (RoutedCommand)binding.Command;
                        check(command.CanExecute(null, files), $"Alt+{key} is available for the selected image");
                        command.Execute(null, files);
                        check(Row(paths[0]).Rank == (key == Key.Down ? 2 : 1), $"Alt+{key} updates the displayed and submitted page order");
                    }
                    var duplicate = Path.Combine(parent.FullName, "P001.JPG");
                    File.Copy(paths[0], duplicate);
                    var folders = browser.Folders;
                    var parentRow = Descendants(folders).OfType<ListBoxItem>().Single(item => AutomationProperties.GetName(item) == "스캔본");
                    parentRow.IsSelected = true;
                    await Wait(() => files.Items.Cast<ImageUploadBrowser.FileRow>().Any(r => r.Path == extra));
                    check(Row(extra).Name == "가.png" && browser.SelectedPaths.SequenceEqual([paths[0],paths[2],paths[1]]),
                        "Browsing to another folder preserves queued pages and displays Korean names in composed form");
                    Toggle(duplicate);
                    check(browser.SelectedPaths.SequenceEqual([paths[0],paths[2],paths[1]]) && Descendants(dialog).OfType<TextBlock>().Any(t => t.IsVisible && t.Text.Contains("이름이 같은 파일")),
                        "Adding a duplicate upload name from another folder leaves the existing image order intact");
                    Toggle(extra);
                    submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                    void Bounds(FrameworkElement control, int x, int y, int w, int h, string name)
                    {
                        var point = control.TranslatePoint(new Point(), dialog);
                        check(Math.Abs(point.X-x)<=1 && Math.Abs(point.Y-y)<=1 && Math.Abs(control.ActualWidth-w)<=1 && Math.Abs(control.ActualHeight-h)<=1,
                            $"Image {name}: ({point.X},{point.Y}) {control.ActualWidth}x{control.ActualHeight} matches Figma ({x},{y}) {w}x{h}");
                    }
                }
                catch (Exception ex) { failure = ex; dialog.Close(); }
            }), DispatcherPriority.ApplicationIdle);
            var accepted = dialog.ShowDialog();
            if (failure is not null) throw new InvalidOperationException("Image upload frame check failed", failure);
            check(accepted == true && dialog.Paths.SequenceEqual([paths[0],paths[2],paths[1],extra]), "Confirmed upload includes the ordered files from both folders");

            var invalid = Path.Combine(folder.FullName, "broken.png"); File.WriteAllText(invalid, "unreadable");
            var bad = new ImageOrderDialog([invalid, paths[0]]); bad.Show();
            try
            {
                var browser = Descendants(bad).OfType<ImageUploadBrowser>().Single();
                await Wait(() => browser.Details.Text.Contains("미리보기를 읽을 수 없습니다"));
                check(browser.Preview.Source is null, "An unreadable image displays a preview error without stale pixels");
                browser.Files.SelectedIndex = 1;
                await Wait(() => browser.Preview.Source is not null);
                check(browser.Details.Text.Contains("p001.jpg") && !browser.Details.Text.Contains("없습니다"), "Selecting a readable image recovers after preview failure");
            }
            finally { bad.Close(); }
        }
        finally { root.Delete(true); }
    }

    private static async Task Wait(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(7);
        while (!ready() && DateTime.UtcNow < deadline) await Task.Delay(20);
        if (!ready()) throw new TimeoutException("Image browser did not complete the requested operation");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
