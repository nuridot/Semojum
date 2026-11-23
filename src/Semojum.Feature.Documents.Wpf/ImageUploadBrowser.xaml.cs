using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Semojum.Core;
using Semojum.Core.Integration;

namespace Semojum.Desktop.Dialogs;

public partial class ImageUploadBrowser : UserControl
{
    internal sealed record FileRow(string Path, int Rank)
    {
        public string Name => DisplayFileName.Normalize(System.IO.Path.GetFileName(Path));
        public bool Included => Rank > 0;
        public string Number => Included ? Rank.ToString() : "";
        public string ToggleName => Name + (Included ? " 제외" : " 추가");
        public string Help => Included ? $"올릴 순서 {Rank}. Alt와 위·아래 화살표로 이동, 스페이스로 제외." : "스페이스로 추가";
    }
    private sealed record FolderRow(string Path, string Name, Thickness Indent);
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    internal static readonly RoutedUICommand MoveUp = new("위로 이동", nameof(MoveUp), typeof(ImageUploadBrowser));
    internal static readonly RoutedUICommand MoveDown = new("아래로 이동", nameof(MoveDown), typeof(ImageUploadBrowser));
    private readonly List<string> ordered;
    private readonly string[] sources;
    private readonly (string Path, string Name)[] favorites;
    private readonly CancellationToken lifetime;
    private string directory;
    private string[] siblings = [];
    private bool updating;
    private CancellationTokenSource? listing;
    private long previewRequest;
    private string? previewPath;
    private string? dragPath;
    private Point dragStart;

    internal string[] SelectedPaths => ordered.ToArray();
    internal event Action? InclusionChanged;
    internal event Action<string>? Problem;

    internal ImageUploadBrowser(string[] paths, CancellationToken lifetime)
    {
        InitializeComponent();
        this.lifetime = lifetime;
        ordered = paths.ToList();
        sources = paths.Select(p => Path.GetDirectoryName(Path.GetFullPath(p))!).Distinct(PathComparer).ToArray();
        directory = sources.FirstOrDefault() ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        favorites = Favorites();
        Files.InputBindings.Add(new KeyBinding(MoveUp, Key.Up, ModifierKeys.Alt));
        Files.InputBindings.Add(new KeyBinding(MoveDown, Key.Down, ModifierKeys.Alt));
        AddMove(MoveUp, -1); AddMove(MoveDown, 1);
        Files.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.None && Files.SelectedItem is FileRow row)
            { e.Handled = true; Toggle(row); }
        };
        Files.PreviewMouseLeftButtonDown += BeginDrag;
        Files.PreviewMouseMove += ContinueDrag;
        Files.DragOver += (_, e) => { e.Effects = OwnDrag(e) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
        Files.Drop += DropFile;
        RefreshFolders([]); RefreshFiles(paths.FirstOrDefault());
        Loaded += (_, _) => Navigate(directory);
    }

    private static (string Path, string Name)[] Favorites()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders",
            "{374DE290-123F-4565-9164-39C4925E467B}", null) as string;
        return [(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "바탕 화면"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "문서"),
                (string.IsNullOrWhiteSpace(downloads) ? Path.Combine(profile, "Downloads") : Environment.ExpandEnvironmentVariables(downloads), "다운로드")];
    }

    private void RefreshFolders(string[] children)
    {
        var rows = new List<FolderRow>(); var seen = new HashSet<string>(PathComparer);
        void Add(string path, string name, int indent)
        {
            if (!string.IsNullOrEmpty(path) && seen.Add(path)) rows.Add(new(path, name, new(indent, 0, 0, 0)));
        }
        foreach (var (path, name) in favorites) Add(path, name, 8);
        string Label(string path) => DisplayFileName.Normalize(Path.GetFileName(path) is { Length: > 0 } name ? name : path);
        var parent = Directory.GetParent(directory)?.FullName;
        if (parent is not null && !favorites.Any(f => PathComparer.Equals(f.Path, directory))) Add(parent, Label(parent), 20);
        Add(directory, Label(directory), 32);
        foreach (var source in sources) Add(source, Label(source), 20);
        foreach (var child in children) Add(child, Label(child), 32);
        var hadFocus = Folders.IsKeyboardFocusWithin;
        updating = true;
        try { Folders.ItemsSource = rows; Folders.SelectedItem = rows.FirstOrDefault(f => PathComparer.Equals(f.Path, directory)); }
        finally { updating = false; }
        if (hadFocus) FocusSelection(Folders);
    }

    private static void FocusSelection(ListBox list)
    {
        if (list.SelectedItem is null) { list.Focus(); return; }
        list.ScrollIntoView(list.SelectedItem); list.UpdateLayout();
        if (list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem) is ListBoxItem item) item.Focus();
    }

    private void FolderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!updating && Folders.SelectedItem is FolderRow folder && !PathComparer.Equals(folder.Path, directory)) Navigate(folder.Path);
    }

    private async void Navigate(string path)
    {
        listing?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        listing = request;
        try
        {
            directory = path; siblings = []; Problem?.Invoke("");
            RefreshFolders([]); RefreshFiles((Files.SelectedItem as FileRow)?.Path);
            var (files, folders) = await Task.Run(() =>
            {
                List<string> images = [], directories = [];
                foreach (var entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
                {
                    request.Token.ThrowIfCancellationRequested();
                    if (entry.Attributes.HasFlag(FileAttributes.Hidden) || entry.Attributes.HasFlag(FileAttributes.System)) continue;
                    if (entry is DirectoryInfo) directories.Add(entry.FullName);
                    else if (entry.Extension.ToLowerInvariant() is ".jpg" or ".jpeg" or ".png") images.Add(entry.FullName);
                }
                images.Sort(PathComparer); directories.Sort(PathComparer);
                return (images.ToArray(), directories.ToArray());
            }, request.Token);
            if (request.IsCancellationRequested) return;
            siblings = files; RefreshFolders(folders);
            RefreshFiles((Files.SelectedItem as FileRow)?.Path ?? files.FirstOrDefault());
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { if (!request.IsCancellationRequested) Problem?.Invoke("폴더를 읽을 수 없습니다: " + path); }
        finally { if (ReferenceEquals(listing, request)) listing = null; }
    }

    private void RefreshFiles(string? selected)
    {
        var rows = ordered.Select((p, i) => new FileRow(p, i + 1)).ToList();
        var included = ordered.Select(Path.GetFullPath).ToHashSet(PathComparer);
        rows.AddRange(siblings.Where(p => !included.Contains(p)).Select(p => new FileRow(p, 0)));
        var hadFocus = Files.IsKeyboardFocusWithin;
        updating = true;
        try
        {
            Files.ItemsSource = rows;
            Files.SelectedItem = rows.FirstOrDefault(r => PathComparer.Equals(r.Path, selected)) ?? rows.FirstOrDefault();
        }
        finally { updating = false; }
        if (hadFocus) FocusSelection(Files);
        UpdatePreview();
        CommandManager.InvalidateRequerySuggested();
    }

    private void ToggleClicked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: FileRow row }) Toggle(row);
        e.Handled = true;
    }

    private void Toggle(FileRow row)
    {
        if (row.Included) ordered.RemoveAt(row.Rank - 1);
        else
        {
            try { UploadFileReader.ValidateUniqueFileNames([.. ordered, row.Path]); }
            catch (ArgumentException ex) { Problem?.Invoke(ex.Message); RefreshFiles(row.Path); return; }
            ordered.Add(row.Path);
        }
        Problem?.Invoke(""); RefreshFiles(row.Path); InclusionChanged?.Invoke();
    }

    private void AddMove(ICommand command, int delta) => Files.CommandBindings.Add(new CommandBinding(command,
        (_, _) => { if (Files.SelectedItem is FileRow row) Move(row.Path, row.Rank - 1 + delta); },
        (_, e) => e.CanExecute = Files.SelectedItem is FileRow { Included: true } r && r.Rank + delta >= 1 && r.Rank + delta <= ordered.Count));

    private void Move(string path, int index)
    {
        var from = ordered.FindIndex(p => PathComparer.Equals(p, path));
        if (from < 0 || index < 0 || index >= ordered.Count || from == index) return;
        ordered.RemoveAt(from); ordered.Insert(index, path); RefreshFiles(path);
        Files.ScrollIntoView(Files.SelectedItem);
    }

    private void BeginDrag(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(Files); dragPath = null;
        if (e.OriginalSource is not DependencyObject origin || ItemsControl.ContainerFromElement(Files, origin) is not ListBoxItem { Content: FileRow { Included: true } row } container) return;
        for (var element = origin; element is Visual && element != container; element = VisualTreeHelper.GetParent(element))
            if (element is CheckBox) return;
        dragPath = row.Path;
    }

    private void ContinueDrag(object sender, MouseEventArgs e)
    {
        if (dragPath is null || e.LeftButton != MouseButtonState.Pressed) return;
        var position = e.GetPosition(Files);
        if (Math.Abs(position.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var data = new DataObject(); data.SetData("Semojum.ImageOrder.List", Files); data.SetData("Semojum.ImageOrder.Path", dragPath);
        dragPath = null; DragDrop.DoDragDrop(Files, data, DragDropEffects.Move);
    }

    private bool OwnDrag(DragEventArgs e) => ReferenceEquals(e.Data.GetData("Semojum.ImageOrder.List"), Files)
        && e.Data.GetData("Semojum.ImageOrder.Path") is string path && ordered.Contains(path, PathComparer);

    private void DropFile(object sender, DragEventArgs e)
    {
        if (!OwnDrag(e)) return;
        var path = (string)e.Data.GetData("Semojum.ImageOrder.Path");
        var target = e.OriginalSource is DependencyObject origin ? ItemsControl.ContainerFromElement(Files, origin) as ListBoxItem : null;
        var index = target?.Content is FileRow { Included: true } row ? row.Rank - 1 + (e.GetPosition(target).Y >= target.ActualHeight / 2 ? 1 : 0) : ordered.Count;
        if (index > ordered.IndexOf(path)) index--;
        Move(path, index); e.Handled = true;
    }

    private void FileChanged(object sender, SelectionChangedEventArgs e) { if (!updating) UpdatePreview(); }
    private async void UpdatePreview()
    {
        var path = (Files.SelectedItem as FileRow)?.Path;
        if (PathComparer.Equals(path, previewPath)) return;
        previewPath = path; var id = ++previewRequest; Preview.Source = null; Details.Text = "";
        PreviewFrame.Visibility = path is null ? Visibility.Hidden : Visibility.Visible;
        if (path is null) return;
        try
        {
            var result = await Task.Run(() => ReadImage(path, 640), lifetime);
            if (lifetime.IsCancellationRequested || id != previewRequest) return;
            Preview.Source = result.Bitmap;
            string size = result.Bytes >= 1024 * 1024 ? $"{result.Bytes / (1024d * 1024):0.#}MB" : $"{Math.Max(1, (result.Bytes + 1023) / 1024)}KB";
            Details.Text = $"{DisplayFileName.Normalize(Path.GetFileName(path))} · {result.Width} × {result.Height} · {size}";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (IsImageError(ex))
        { if (!lifetime.IsCancellationRequested && id == previewRequest) Details.Text = "미리보기를 읽을 수 없습니다: " + DisplayFileName.Normalize(Path.GetFileName(path)); }
    }

    private static bool IsImageError(Exception ex) => ex is IOException or UnauthorizedAccessException or NotSupportedException
        or FileFormatException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException;
    private static (BitmapSource Bitmap, int Width, int Height, long Bytes) ReadImage(string path, int limit)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > JobClient.MaximumUploadBytes) throw new IOException("100 MB를 넘는 이미지입니다.");
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
        var width = frame.PixelWidth; var height = frame.PixelHeight; stream.Position = 0;
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (width >= height) bitmap.DecodePixelWidth = Math.Min(width, limit);
        else bitmap.DecodePixelHeight = Math.Min(height, limit);
        bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
        return (bitmap, width, height, stream.Length);
    }

    private void ThumbnailLoaded(object sender, RoutedEventArgs e) => ShowThumbnail((Image)sender);
    private void ThumbnailChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        var image = (Image)sender;
        if (image.IsLoaded) ShowThumbnail(image);
    }
    private async void ShowThumbnail(Image image)
    {
        image.Source = null;
        if (image.DataContext is not FileRow row || lifetime.IsCancellationRequested) return;
        try
        {
            var result = await Task.Run(() => ReadImage(row.Path, 48), lifetime);
            if (!lifetime.IsCancellationRequested && image.IsLoaded && ReferenceEquals(image.DataContext, row)) image.Source = result.Bitmap;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (IsImageError(ex)) { }
    }

    private void ClipColumns(object sender, SizeChangedEventArgs e) =>
        Columns.Clip = new RectangleGeometry(new Rect(e.NewSize), 7, 7);
}
