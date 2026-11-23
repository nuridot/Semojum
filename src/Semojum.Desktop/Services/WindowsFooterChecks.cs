using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;
using SharpVectors.Converters;

namespace Semojum.Desktop.Services;

internal static class WindowsFooterChecks
{
    internal static async Task RunAsync(string directory, Action<bool,string> check)
    {
        var folder = Directory.CreateDirectory(Path.Combine(directory, "footers")).FullName;
        var window = new MainWindow(smoke: true, testWithoutAccount: true);
        window.Show();
        try
        {
            var model = window.Session;
            model.Layout = model.Layout with { FooterAlign = "left", StartPage = 13, CoverPages = 2 };
            model.FooterText = "기본 꼬리말";
            await Input(async d =>
            {
                var start = Field<TextBox>(d,"시작 면 번호");
                var text = Field<TextBox>(d,"내용");
                var center = Field<RadioButton>(d,"정렬 가운데");
                check(d.ActualWidth == 520 && d.ActualHeight == 262 && center.IsChecked == true && text.MaxLength == 200,
                    "Footer input matches the 520x262 frame, 200-character limit and default center alignment");
                check(start.Text == "13", "A document with unnumbered covers starts footer choices at its first printed number");
                start.Text = "12";
                Press(Field<RepeatButton>(d,"시작 면 번호 늘리기"));
                check(start.Text == "13", "The original Figma spinner advances the printed start number");
                text.Text = "사회 · 문화 1";
                Bounds(d,text,163,69,316,32);
                Bounds(d,start,163,145,96,32);
                Bounds(d,Action(d,"확인"),407,213,88,32);
                Icons(d,"form-close","layout-step-up","layout-step-down");
                await WindowsSmoke.RenderAsync(d,Path.Combine(folder,"input.png"));
                start.Text = "0";
                Press(Action(d,"확인"));
                check(d.IsVisible && d.Result is null, "Invalid printed start numbers leave footer insertion open");
                start.Text = "13";
                Field<RadioButton>(d,"정렬 오른쪽").IsChecked = true;
                Press(Action(d,"확인"));
            });
            check(model.FooterRanges is [{ Start:13, End:null, Text:"사회 · 문화 1", Alignment:"right" }], "The ribbon inserts the chosen text and alignment from print page 13 onward");
            FooterRange[] example = [new(1,12,"Ⅰ 01 사회·문화 현상"), new(13,28,"Ⅰ 02 사회·문화 현상의 탐구"),
                new(29,44,"Ⅱ 01 개인과 사회 구조","center"), new(45,null,"Ⅱ 02 일탈 행동","right")];
            model.FooterRanges = example.ToArray();

            await Manage(async d =>
            {
                var grid = Elements<DataGrid>(d).Single(); var rows = grid.Items.Cast<FooterDialog.Entry>().ToArray();
                check(d.ActualWidth == 720 && d.ActualHeight == 380 && grid.Columns.Count == 5 &&
                    rows.Select(r => r.End).SequenceEqual(["12","28","44","끝"]), "Footer management shows distinct start and end columns with the four measured Figma ranges");
                check(rows.Select(r => r.Alignment).SequenceEqual(["기본","기본","가운데","오른쪽"]), "Inherited alignment is distinct from a row with its own position");
                var first = Row(grid,0);
                Bounds(d,Field<TextBox>(first,"시작 면 번호"),38,95,96,32);
                Bounds(d,Field<TextBox>(first,"꼬리말 내용"),222,95,300,32);
                Bounds(d,Field<ComboBox>(first,"꼬리말 정렬"),530,95,132,32);
                Bounds(d,Action(d,"모두 지우기"),25,331,88,32);
                Bounds(d,Action(d,"확인"),607,331,88,32);
                Icons(d,"form-close","layout-step-up","layout-step-down","line-next","table-trash","table-plus");
                await WindowsSmoke.RenderAsync(d,Path.Combine(folder,"manage.png"));
                var next = Row(grid,1);
                Press(Field<RepeatButton>(next,"시작 면 번호 늘리기"));
                check(rows[1].Start == "14" && rows[0].End == "13", "Changing a bound row spinner recalculates the previous row's end");
                Press(Field<RepeatButton>(next,"시작 면 번호 줄이기"));
                check(rows[1].Start == "13" && rows[0].End == "12", "Repeated spinner use keeps its source binding");
                Press(Action(d,"모두 지우기"));
                check(grid.Items.Count == 0 && Field<Button>(d,"꼬리말 줄 더하기").IsVisible, "Clear-all removes the editable rows and keeps the add action");
                Press(Action(d,"취소"));
            });
            check(model.FooterRanges.SequenceEqual(example) && model.FooterText == "기본 꼬리말", "Cancelling a clear-all operation leaves the document intact");

            await Manage(async d =>
            {
                var grid = Elements<DataGrid>(d).Single();
                var first = Row(grid,0); Field<ComboBox>(first,"꼬리말 정렬").SetCurrentValue(Selector.SelectedItemProperty,"오른쪽");
                Field<TextBox>(first,"꼬리말 내용").SetCurrentValue(TextBox.TextProperty,"첫 단원");
                var from = Field<TextBox>(Row(grid,1),"시작 면 번호");
                from.SetCurrentValue(TextBox.TextProperty,"1");
                Press(Action(d,"확인"));
                check(d.IsVisible && model.FooterRanges.SequenceEqual(example), "A duplicate start is rejected before any document change");
                from.SetCurrentValue(TextBox.TextProperty,"15");
                var add = Field<Button>(d,"꼬리말 줄 더하기");
                Press(add);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                grid.UpdateLayout();
                check(grid.SelectedItem is FooterDialog.Entry { Start:"46" } && grid.Items.Count == 5, "An added row is selected immediately after the last start");
                var last = Row(grid,4);
                Field<TextBox>(last,"꼬리말 내용").SetCurrentValue(TextBox.TextProperty,"연습");
                Press(Field<Button>(Row(grid,3),"이 꼬리말 삭제"));
                check(grid.Items.Count == 4 && grid.Items.Cast<FooterDialog.Entry>().Last().Text == "연습", "A row's delete action targets that row even when another is selected");
                Press(Action(d,"확인"));
            });
            check(model.Layout.FooterAlign == "left" && model.FooterRanges.SequenceEqual(new FooterRange[] {
                new(1,14,"첫 단원","right"),new(15,28,example[1].Text),new(29,45,example[2].Text,"center"),new(46,null,"연습")
            }), "Editing a row's alignment preserves the document default and saves its exact new ranges");
            var path = Path.Combine(folder,"ranges.smj");
            await SmjFile.SaveAsync(path, SmjFile.Snapshot(model));
            var restored = await SmjFile.LoadAsync(path);
            check(restored.FooterRanges.SequenceEqual(model.FooterRanges) && restored.Layout.FooterAlign == "left" && restored.FooterText == model.FooterText,
                "Reopening the SMJ preserves the footer words, every inclusive boundary and inherited alignment");

            model.FooterText = "복원"; model.FooterRanges = [new(3,5,"이전 장","right"),new(9,12,"다음 장","center")];
            await Manage(d =>
            {
                var grid = Elements<DataGrid>(d).Single();
                var rows = grid.Items.Cast<FooterDialog.Entry>().ToArray();
                check(rows.Select(r => r.Start).SequenceEqual(["1","3","6","9","13"]) &&
                    rows[2] is {Text:"복원", Alignment:"기본"} && rows[^1].End == "끝", "Legacy finite ranges expose the inherited restoration before, between and after spans");
                Press(Action(d,"확인"));
                return Task.CompletedTask;
            });
            check(model.FooterRanges.SequenceEqual([new FooterRange(3,5,"이전 장","right"),new FooterRange(9,12,"다음 장","center")]) &&
                model.FooterText == "복원", "Confirming without edits preserves the original finite spans and the document fallback");
            await Manage(async d =>
            {
                var grid = Elements<DataGrid>(d).Single();
                for (var i=0; i<12; i++) Press(Field<Button>(d,"꼬리말 줄 더하기"));
                d.Width = 480; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); d.UpdateLayout();
                var horizontal = Field<ScrollViewer>(d,"꼬리말 표 가로 스크롤");
                var vertical = Elements<ScrollViewer>(grid).First();
                horizontal.ScrollToRightEnd();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                check(vertical.ScrollableHeight > 0 && horizontal.HorizontalOffset > 0 && Field<Button>(d,"꼬리말 줄 더하기").IsVisible,
                    "A narrow table can reach the rightmost delete actions and scroll long lists vertically");
                Press(Action(d,"모두 지우기"));
                Press(Action(d,"확인"));
            });
            check(model.FooterRanges.Length == 0 && model.FooterText.Length == 0, "Confirming clear-all removes even the document fallback text");

            var sparse = Enumerable.Range(1,1000).Select(i => new FooterRange(i*2,i*2,"단원 " + i,"right")).ToArray();
            model.FooterText = "기본"; model.FooterRanges = sparse;
            await Manage(d =>
            {
                check(Elements<DataGrid>(d).Single().Items.Count == 2001, "All 1000 finite legacy ranges and their restoration rows can be opened");
                Press(Action(d,"확인"));
                return Task.CompletedTask;
            });
            check(model.FooterRanges.SequenceEqual(sparse) && model.FooterText == "기본", "Saving a maximum-size sparse legacy document preserves all printed ranges");
            await Manage(d =>
            {
                var grid = Elements<DataGrid>(d).Single();
                Field<TextBox>(Row(grid,1),"꼬리말 내용").SetCurrentValue(TextBox.TextProperty,"수정한 단원");
                Press(Action(d,"확인"));
                return Task.CompletedTask;
            });
            check(model.FooterRanges.Length == 1000 && model.FooterRanges[0] == (sparse[0] with { Text = "수정한 단원" }) &&
                model.FooterRanges.Skip(1).SequenceEqual(sparse.Skip(1)) && model.FooterText == "기본",
                "Editing a maximum-size legacy file stores all real ranges and retains the fallback between them");

            await VerifyHeaderAsync(window, folder, check);
            await VerifyPrintedFaceAsync(window, check);
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }

        Task Input(Func<FooterInputDialog,Task> inspect) =>
            WindowsSmoke.WithModal<FooterInputDialog>(() => window.ExecuteAsync("footerInsert"),inspect);
        Task Manage(Func<FooterDialog,Task> inspect) =>
            WindowsSmoke.WithModal<FooterDialog>(() => window.ExecuteAsync("footer"),inspect);
        void Icons(Window d,params string[] expected)
        {
            var actual=Elements<SvgViewbox>(d).Where(i => i.IsVisible && i.Drawings is {Bounds.IsEmpty:false}).Select(i=>i.Source.ToString()).ToArray();
            check(expected.All(name=>actual.Any(path=>path.EndsWith("/"+name+".svg"))),d.Title + " renders every original Figma glyph");
        }
        void Bounds(Window d,FrameworkElement element,int x,int y,int w,int h)
        {
            var p = element.TranslatePoint(new Point(),d);
            check(Math.Abs(x-p.X)<=1 && Math.Abs(y-p.Y)<=1 && Math.Abs(w-element.ActualWidth)<=1 && Math.Abs(h-element.ActualHeight)<=1,
                $"{d.Title} {AutomationProperties.GetName(element)} at {p.X},{p.Y} {element.ActualWidth}x{element.ActualHeight} matches {x},{y} {w}x{h}");
        }
    }
    private static async Task VerifyHeaderAsync(MainWindow window, string folder, Action<bool,string> check)
    {
        var editor = window.PageEditor.Visibility == Visibility.Visible ? window.PageEditor.Editor : window.SourceEditor;
        editor.Select(0,0);
        var original = editor.Text;
        const string title = "Ⅰ 01 사회·문화 현상";
        await WindowsSmoke.WithModal<HeaderInputDialog>(() => window.ExecuteAsync("headerInsert"), async dialog =>
        {
            var input = Field<TextBox>(dialog, "머리말 내용");
            input.Text = title;
            var origin = input.TranslatePoint(new(), dialog);
            check(dialog.ActualWidth == 520 && dialog.ActualHeight == 218 && origin == new Point(163,69)
                && input.ActualWidth == 316 && input.ActualHeight == 32 && input.MaxLength == 200,
                "The header ribbon opens the 520x218 Figma form with its measured 200-character field");
            await WindowsSmoke.RenderAsync(dialog, Path.Combine(folder,"header.png"));
            input.Text = new string('가',201);
            Press(Action(dialog,"확인"));
            check(dialog.IsVisible, "A pasted or selected overlength header stays editable");
            input.Text = "<!머리말>중첩<!/머리말>";
            Press(Action(dialog,"확인"));
            check(dialog.IsVisible, "Nested header tags are rejected before modifying the document");
            Press(Action(dialog,"취소"));
        });
        check(editor.Text == original, "Cancel after invalid header input preserves the original source");
        await WindowsSmoke.WithModal<HeaderInputDialog>(() => window.ExecuteAsync("headerInsert"), dialog =>
        {
            Field<TextBox>(dialog,"머리말 내용").Text = title;
            Field<RadioButton>(dialog,"정렬 오른쪽").IsChecked = true;
            Press(Action(dialog,"확인"));
            return Task.CompletedTask;
        });
        check(editor.Text == BrailleTags.InsertHeader(title,"right") + original, "The ribbon inserts the exact right-aligned header into the real editor");
    }

    private static async Task VerifyPrintedFaceAsync(MainWindow window, Action<bool,string> check)
    {
        await using var sidecar = EngineRuntime.Create();
        var engine = new LocalBrailleEngine(sidecar);
        var model = window.Session;
        model.Blocks.Clear(); model.FooterRanges = []; model.FooterText = "";
        model.Layout = new(Columns:16, Lines:4, StartPage:13, CoverPages:1, Footer:"모든 페이지", PageNumbers:"출력 안 함",
            ShowSourcePageNumber:false, ShowBraillePageNumber:false, ShowChangeLine:false, FooterAlign:"left");
        model.Add(new EditorBlock { Id=1, Page=1, Source="표지", Braille="⠁", HasManualBraille=true });
        model.Add(new EditorBlock { Id=2, Page=2, Source="본문", Braille=string.Join("\n",Enumerable.Repeat("⠁",24)), HasManualBraille=true });
        window.Braille.ComposeGridAsync = doc => engine.ComposeAsync(SmjFile.Snapshot(doc).Manifest);
        check(await window.Braille.EnsureCurrentGridAsync(model), "The printed face picker uses a current grid composed by the packaged engine");
        var before = await engine.ComposeAsync(SmjFile.Snapshot(model).Manifest);
        var pages = before.Pages!;
        var selected = Array.FindIndex(pages,p => p.Number == 15)+1;
        check(pages[0].Number is null && selected > 0 && selected != 15 && window.Braille.GridView.GoToFace(selected),
            "Grid navigation reaches printed face 15 past an unnumbered cover and a non-default start");
        await WindowsSmoke.WithModal<FooterInputDialog>(() => window.ExecuteAsync("footerInsert"), d =>
        {
            check(Field<TextBox>(d,"시작 면 번호").Text == "15", "Footer insertion proposes the printed number of the browsed grid face");
            Field<TextBox>(d,"내용").Text = "나";
            Field<RadioButton>(d,"정렬 오른쪽").IsChecked = true;
            Press(Action(d,"확인"));
            return Task.CompletedTask;
        });
        var composed = await engine.ComposeAsync(SmjFile.Snapshot(model).Manifest);
        var brf = await engine.ReadBrfAsync(composed.Brf);
        var mark = (await engine.TranslateElementAsync("나",new(Type:"header_footer"),model.Options)).Cells.Trim('⠀');
        var at = Array.FindIndex(composed.Pages!,p => p.Number == 15);
        check(at > 0 && !string.IsNullOrEmpty(mark) && brf[at][^1].TrimEnd('⠀').EndsWith(mark) &&
            brf[at][^1].IndexOf(mark,StringComparison.Ordinal) >= 12 && !brf[at-1][^1].Contains(mark),
            "Exported BRF puts the right-aligned footer on printed face 15 and leaves the preceding face untouched");
        window.Braille.ComposeGridAsync = null;
    }

    private static DataGridRow Row(DataGrid grid, int index)
    {
        grid.ScrollIntoView(grid.Items[index]); grid.UpdateLayout();
        return (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(index);
    }
    private static Button Action(Window window,string caption) => Elements<Button>(window).Single(b=>Equals(b.Content,caption));
    private static void Press(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static T Field<T>(DependencyObject root,string name) where T:DependencyObject => Elements<T>(root).Single(c=>AutomationProperties.GetName(c)==name);
    private static IEnumerable<T> Elements<T>(DependencyObject root) where T:DependencyObject
    {
        for (var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);
            if (child is T element) yield return element;
            foreach(var item in Elements<T>(child)) yield return item;
        }
    }
}
