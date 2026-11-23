using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop.Services;

internal static class WindowsPrintPreviewFrameChecks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var previewFolder = Directory.CreateDirectory(Path.Combine(directory, "preview-pages")).FullName;
        var printFolder = Directory.CreateDirectory(Path.Combine(directory, "braille-print")).FullName;
        string[] sample = ["2 (한국지리) 사회탐구 영역", "6. 그래프는 지도에 표시된 네 지역 (가)~(라)의", "기후 특성을 나타낸 것이다. 이에 대한 설명은?", "① (가)는 겨울철 강수량 비율이 가장 높다.", "② (나)는 연교차가 가장 크다.", "③ (다)는 (라)보다 최한월 평균 기온이 낮다.", "그림: 지도에 (가)~(라) 네 지점 표시.", "④ (라)는 (가)보다 위도가 높다.", "⑤ (가)~(라) 모두 ⬛ 집중률이 높다."];
        string[] dots = ["⠼⠃⠀⠥⠥⠠⠕⠨⠕⠐⠥⠀⠠⠊⠚⠺⠞⠥", "⠼⠋⠲⠀⠥⠍⠐⠗⠨⠞⠀⠨⠕⠊⠥⠕⠀⠙", "⠠⠝⠀⠉⠮⠀⠨⠕⠥⠍⠀⠦⠫⠴⠭⠦⠐⠣", "⠼⠁⠀⠦⠫⠴⠒⠀⠥⠜⠥⠲⠹⠀⠥⠴⠠⠥", "⠼⠃⠀⠦⠉⠴⠒⠀⠡⠥⠺⠰⠁⠥⠍⠀⠥⠍", "⠼⠉⠀⠦⠊⠴⠒⠀⠦⠐⠣⠴⠘⠕⠊⠥⠀⠝"];
        var text = string.Join("\n", Enumerable.Range(0, 22).Select(i => sample[i % sample.Length]));
        var originals = Enumerable.Range(1, 128).Select(i => new SourcePreviewPage(i, text + (i == 13 ? " 다음 원본" : ""))).ToArray();
        var faces = Enumerable.Range(1, 640).Select(i => Enumerable.Range(0, 22).Select(j => dots[j % dots.Length]).ToArray()).ToArray();
        faces[31][21] += "⠿";
        const string title = "2026_수능특강_사회문화.smj";
        var selectedForPrint = 0;
        var preview = new DocumentPreviewWindow(originals, faces, title, true, 12, 31, true, "입력 한글", false, false,
            w => selectedForPrint = w.Face);
        try
        {
            preview.Show();
            preview.Width = 1440;
            preview.Height = 1000;
            await WindowsSmoke.RenderAsync(preview, Path.Combine(previewFolder, "text.png"));
            var sheets = Children<Border>(preview).Where(b => b.Child is RichTextBox).ToArray();
            check(sheets.Length == 2 && preview.ActualWidth == 1440 && preview.ActualHeight == 1000, "Preview has a 1440x1000 frame and two printable sheets");
            for (var i = 0; i < sheets.Length; i++)
            {
                var position = sheets[i].TranslatePoint(new Point(), preview);
                check(Math.Abs(position.X - (188 + 544*i)) <= 1 && Math.Abs(position.Y - 196) <= 1 && sheets[i].ActualWidth == 520 && sheets[i].ActualHeight == 700,
                    $"Preview sheet {i+1} at ({position.X},{position.Y}) matches its 520x700 Figma rectangle");
            }
            string Read(int i)
            {
                var doc = ((RichTextBox)sheets[i].Child).Document;
                return new TextRange(doc.ContentStart, doc.ContentEnd).Text;
            }
            check(!Read(0).Contains("다음 원본") && Read(1).Contains("다음 원본"), "Facing source pages display distinct original contents");
            void Click(string name) => ButtonNamed(preview, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Click("점자 페이지");
            await WindowsSmoke.RenderAsync(preview, Path.Combine(previewFolder, "braille.png"));
            check(preview.Face == 31 && !Read(0).Contains("⠿") && Read(1).Contains("⠿"), "Braille spread uses adjacent composed faces and an independent position");
            KeyPress(preview, Key.PageDown);
            Click("인쇄");
            check(selectedForPrint == 33, "Printing from the preview receives the browsed braille face");
            KeyPress(preview, Key.End);
            check(preview.Face == 640 && !sheets[1].IsVisible, "The final face has no extra sheet");
            Click("텍스트 페이지");
            check(Children<TextBlock>(preview).Any(t => t.Text == "12 / 128쪽"), "Returning to source restores the original page");
        }
        finally { preview.Close(); }

        faces[30] = ["⠼⠃⠀⠥⠥⠠⠕⠨⠕⠐⠥⠀⠠⠊⠚⠺⠞⠥⠍", "⠼⠋⠲⠀⠥⠍⠐⠗⠨⠞⠀⠨⠕⠊⠥⠕⠀⠙⠣⠠⠕⠙⠺⠒", "⠠⠝⠀⠉⠮⠀⠨⠕⠥⠍⠀⠦⠫⠴⠭⠦⠐⠣⠴⠺⠀⠥⠕⠚⠥", "⠼⠁⠀⠦⠫⠴⠒⠀⠥⠜⠥⠲⠹⠀⠥⠴⠠⠥⠐⠱⠀⠘⠊⠯⠕", "⠼⠃⠀⠦⠉⠴⠒⠀⠡⠥⠺⠰⠁⠥⠍⠀⠥⠍⠁⠲", "⠼⠉⠀⠦⠊⠴⠒⠀⠦⠐⠣⠴⠘⠕⠊⠥⠀⠝⠚⠝⠯", "⠼⠃⠀⠥⠥⠠⠕⠨⠕⠐⠥⠀⠠⠊⠚⠺⠞⠥⠍", "⠼⠋⠲⠀⠥⠍⠐⠗⠨⠞⠀⠨⠕⠊⠥⠕⠀⠙⠣⠠⠕⠙⠺⠒", "⠠⠝⠀⠉⠮⠀⠨⠕⠥⠍⠀⠦⠫⠴⠭⠦⠐⠣⠴⠺⠀⠥⠕⠚⠥", "⠼⠁⠀⠦⠫⠴⠒⠀⠥⠜⠥⠲⠹⠀⠥⠴⠠⠥⠐⠱⠀⠘⠊⠯⠕", "⠼⠃⠀⠦⠉⠴⠒⠀⠡⠥⠺⠰⠁⠥⠍⠀⠥⠍⠁⠲", "⠼⠉⠀⠦⠊⠴⠒⠀⠦⠐⠣⠴⠘⠕⠊⠥⠀⠝⠚⠝⠯", "⠼⠃⠀⠥⠥⠠⠕⠨⠕⠐⠥⠀⠠⠊⠚⠺⠞⠥⠍", "⠼⠋⠲⠀⠥⠍⠐⠗⠨⠞⠀⠨⠕⠊⠥⠕⠀⠙⠣⠠⠕⠙⠺⠒", "⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠼⠉⠁"];
        var raw = string.Concat(Enumerable.Range(1, 640).Select(i => (i == 32 ? "" : $"LINE {i}\r\n") + "\f"));
        var inspection = new BraillePreviewDialog(faces, raw, new PageLayout(), title, 31);
        try
        {
            inspection.Show();
            await WindowsSmoke.RenderAsync(inspection, Path.Combine(printFolder, "faces.png"));
            check(inspection.ActualWidth == 720 && inspection.ActualHeight == 632 && inspection.Page == 31, "Face inspection matches the 720x632 frame at the requested face");
            ButtonNamed(inspection, "다음").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(inspection.Page == 32, "Next advances the inspected face");
            ButtonNamed(inspection, "이전").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var number = Children<TextBox>(inspection).Single(b => AutomationProperties.GetName(b) == "이동할 점자 면");
            number.Text = "641";
            ButtonNamed(inspection, "이동").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(inspection.Page == 31 && Children<TextBlock>(inspection).Any(t => t.Text.Contains("640면 안에서")), "An invalid face leaves the inspection on the current page");
            number.Text = "4";
            KeyPress(number, Key.Enter);
            check(inspection.Page == 4, "Pressing Enter jumps to the supplied face");
        }
        finally { inspection.Close(); }
        var range = new ExportRange(ExportScope.BraillePages, 31, 32);
        check(BraillePreviewDialog.ExportContent(raw, faces, range, false) == "LINE 31\r\n\f\f", "BRF range export retains the blank face and exact terminators");
        check(BraillePreviewDialog.ExportContent(raw, faces, range, true) == string.Join("\r\n", faces[30]) + "\r\n\f" + string.Join("\r\n", faces[31]) + "\r\n\f",
            "TXT range export contains both selected Unicode faces with their row boundaries");

        var queues = new TaskCompletionSource<(string[] Queues, string? Default)>();
        var jobs = new List<(string Name, string Title, string Payload, int Copies)>();
        var simulateFailure = true;
        var print = new BraillePrintDialog(640, () => queues.Task, (printer, first, last, copies) =>
        {
            if (simulateFailure) throw new IOException("검증용 대기열 오류");
            return RawBraillePrinter.SendRangeAsync(raw, title, printer, first, last, copies,
                (q, t, b, c) => jobs.Add((q,t,Encoding.ASCII.GetString(b),c)));
        });
        Exception? failure = null;
        print.ContentRendered += (_, _) => print.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                var submit = ButtonNamed(print, "인쇄");
                check(!submit.IsEnabled, "Printing is unavailable until queues have loaded");
                queues.SetResult((["테스트", "Index Everest-D V5"], "Index Everest-D V5"));
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                var combo = Children<ComboBox>(print).Single();
                check(submit.IsEnabled && Equals(combo.SelectedItem, "Index Everest-D V5"), "The system default printer is initially selected");
                TextBox Entry(string name) => Children<TextBox>(print).Single(b => AutomationProperties.GetName(b) == name);
                var start = Entry("시작 면");
                var end = Entry("마지막 면");
                var copies = Entry("인쇄 매수");
                start.Text = "31";
                end.Text = "62";
                await WindowsSmoke.RenderAsync(print, Path.Combine(printFolder, "print.png"));
                check(print.ActualWidth == 480 && print.ActualHeight == 274, "Printer configuration matches the 480x274 Figma frame");
                void Submit() => submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                end.Text = "1"; Submit();
                check(jobs.Count == 0 && print.IsVisible && Children<TextBlock>(print).Any(t => t.Text.Contains("마지막 면을 확인")), "A reversed range is rejected before dispatch");
                end.Text = "32"; copies.Text = "101"; Submit();
                check(jobs.Count == 0 && Children<TextBlock>(print).Any(t => t.Text.Contains("1–100부")), "Excessive copies are rejected before dispatch");
                combo.SelectedIndex = 0; copies.Text = "2"; Submit();
                check(jobs.Count == 0 && print.IsVisible && Children<TextBlock>(print).Any(t => t.Text.Contains("검증용 대기열 오류")), "A queue error retains the configuration for retry");
                simulateFailure = false; Submit();
            }
            catch (Exception ex) { failure = ex; print.Close(); }
        }));
        var submitted = print.ShowDialog();
        if (failure is not null) throw failure;
        check(submitted == true && jobs.Single() == ("테스트", title, "LINE 31\r\n\f\f", 2), "Retry submits the selected queue, range, blank face and copies to the test sink");
        await RawBraillePrinter.SendRangeAsync(raw, title, "test", 1, 640, 1, (_,_,b,_) => check(Encoding.ASCII.GetString(b) == raw, "Selecting all faces preserves the original BRF bytes"));
        var empty = new BraillePrintDialog(1, () => Task.FromResult<(string[], string?)>(([], null)), (_,_,_,_) => throw new Exception());
        try
        {
            empty.Show(); empty.UpdateLayout();
            check(!ButtonNamed(empty, "인쇄").IsEnabled && Children<TextBlock>(empty).Any(t => t.Text.Contains("프린터가 없습니다")), "An empty queue list explains why printing is unavailable");
        }
        finally { empty.Close(); }
    }

    private static Button ButtonNamed(Window window, string name) => Children<Button>(window)
        .Single(b => Equals(b.Content, name) || AutomationProperties.GetName(b) == name && b.Content is StackPanel);

    private static void KeyPress(UIElement control, Key key) => control.RaiseEvent(
        new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(control), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    private static IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T matching) yield return matching;
            foreach (var descendant in Children<T>(child)) yield return descendant;
        }
    }
}
