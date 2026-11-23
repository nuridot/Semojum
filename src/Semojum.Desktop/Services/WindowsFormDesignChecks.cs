using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;
using SharpVectors.Converters;

namespace Semojum.Desktop.Services;

internal static class WindowsFormDesignChecks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var folder = Directory.CreateDirectory(Path.Combine(directory, "braille-forms")).FullName;
        var fixture = new Fixture();
        using var http = new HttpClient(fixture);
        var api = new SemojumApiClient(http);
        await api.LoginAsync("fixture", "fixture");
        var client = new SettingsClient(api);
        var forms = await client.FormsAsync();
        var picker = new FormPickerDialog(forms, forms[0], canEdit: true, canDelete: true); picker.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); picker.UpdateLayout();
            check(picker.Width == 380 && picker.Height == 208, "Five-form picker matches the final Figma 380x208 panel");
            check(HasIcon(picker, "form-picker-close.svg") && HasIcon(picker, "form-picker-edit.svg"), "Form picker uses final Figma close and edit icons");
            check(picker.ChoicesList.Items.Count == 5 && picker.Selected?.FormId == forms[0].FormId, "Form picker retains every individual form and its selected value");
            var deleteButtons = Descendants(picker).OfType<Button>().Where(b => AutomationProperties.GetName(b).EndsWith(" 지우기", StringComparison.Ordinal)).ToArray();
            check(deleteButtons.Length == 5 && deleteButtons.Count(b => b.IsEnabled) == 1
                && deleteButtons.Single(b => b.IsEnabled).ToolTip?.ToString() == "이 양식 지우기"
                && HasIcon(picker, "footer-trash.svg"), "Only a personal form has an enabled delete action in its picker row");
            if (Environment.GetEnvironmentVariable("FORM_PICKER_CAPTURE") is { Length: > 0 } capture) await WindowsSmoke.RenderElementAsync(picker, capture);
        }
        finally { picker.Close(); }

        var deletePicker = new FormPickerDialog(forms, forms[0], canEdit: true, canDelete: true);
        deletePicker.Loaded += (_, _) => deletePicker.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            Descendants(deletePicker).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "중학 수학 (28줄) 지우기")
                .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent))));
        check(deletePicker.ShowDialog() == false && deletePicker.DeleteRequest?.FormId == "user:1" && deletePicker.Selected?.FormId == forms[0].FormId,
            "Deleting an unselected row requests that personal form without changing the selected form");
        var protectedPicker = new FormPickerDialog(forms, forms[0], canDelete: true);
        protectedPicker.Loaded += (_, _) => protectedPicker.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        { protectedPicker.Delete(forms[0]); protectedPicker.Close(); }));
        protectedPicker.ShowDialog();
        check(protectedPicker.DeleteRequest is null, "Built-in forms remain protected even if deletion is invoked directly");


        var personal = forms.Single(f => !f.Builtin);
        var form = new FormDialog(client, new(), new(), forms[2].FormId);
        await WindowsSmoke.WithModal<FormDialog>(() => { form.ShowDialog(); return Task.CompletedTask; }, async window =>
        {
            var list = Descendants(window).OfType<ListBox>().Single();
            await WaitAsync(() => list.SelectedItem is BrailleForm);
            window.UpdateLayout();
            var summary = Descendants(window).OfType<TextBlock>().Where(t => AutomationProperties.GetAutomationId(t).StartsWith("FormSummary"))
                .OrderBy(AutomationProperties.GetAutomationId).ToArray();
            check(list.Items.Count == 5 && Equals(list.SelectedItem, list.Items[2]) && summary.Select(t => t.Text)
                .SequenceEqual(new[] { "26줄 × 32칸", "약자(2급)", "음절", "홀수", "홀수 · 가운데" }), "The selected textbook has its five Figma summary values");
            check(summary.All(t => Equals(t.ToolTip,t.Text) && AutomationProperties.GetName(t).EndsWith(t.Text)), "Summary values are accessible in full");
            Frame(window, 560,326);
            Bounds(window, (FrameworkElement)list.Parent,25,57,220,190);
            Bounds(window, Button(window,"사용자 양식 설정"),25,277,113,32);
            Bounds(window, Button(window,"취소"),351,277,88,32);
            await WindowsSmoke.RenderAsync(window, Path.Combine(folder,"forms.png"));

            list.SelectedItem = null;
            check(summary.All(t => t.Text == "—") && !Button(window,"확인").IsEnabled, "An empty selection clears the summary and disables confirmation");
            list.SelectedIndex = 4;
            check(summary[0].Text == "28줄 × 32칸" && summary[4].Text == "홀수 · 오른쪽", "Choosing a personal form immediately displays its own settings");
            await WindowsSmoke.WithModal<FormEditDialog>(() => Click(window,"사용자 양식 설정"), async editor =>
            {
                Frame(editor,560,446);
                Bounds(editor, Field<TextBox>(editor,"양식 이름"),203,69,316,32);
                Bounds(editor, Field<ComboBox>(editor,"바탕 양식"),203,113,316,32);
                Bounds(editor, Button(editor,"지우기"),25,397,88,32);
                check(Field<TextBox>(editor,"줄 수").Text == "28" && (Field<ComboBox>(editor,"바탕 양식").SelectedItem as BrailleForm)?.FormId == forms[2].FormId,
                    "Opening an existing form keeps its saved overrides and its base");
                check(HasIcon(editor,"conversion-chevron.svg") && Descendants(editor).OfType<SvgViewbox>().Count(i =>
                    i.Source.ToString().Contains("/layout-step-") && i.Drawings is { Bounds.IsEmpty:false }) == 4, "The editor renders its original selector and four stepper icons");
                await WindowsSmoke.RenderAsync(editor, Path.Combine(folder,"custom.png"));
                await WindowsSmoke.WithModal<DeleteFormDialog>(() => Click(editor,"지우기"), async confirm =>
                {
                    Frame(confirm,440,170);
                    Bounds(confirm, Button(confirm,"지우기"),327,121,88,32);
                    check(Descendants(confirm).OfType<TextBlock>().Any(t => t.Text == $"「{personal.Name}」 양식을 지웁니다. 이 양식으로 이미 변환한 문서는 그대로 남습니다."),
                        "The delete confirmation identifies the form and explains what happens to existing documents");
                    await WindowsSmoke.RenderAsync(confirm, Path.Combine(folder,"remove.png"));
                    await Click(confirm,"취소");
                });
                check(editor.IsVisible && fixture.Deletes == 0, "Cancelling deletion leaves the editor and saved form intact");
                Field<RadioButton>(editor,"쪽 번호 넣을 면 짝수").IsChecked = true;
                await Click(editor,"저장");
                await WaitAsync(() => !editor.IsVisible);
            });
            check(fixture.Patches == 1 && summary[3].Text == "짝수" && (list.SelectedItem as BrailleForm)?.FormId == personal.FormId,
                "Saving an even-side override updates the selected personal form and summary once");
            list.SelectedIndex = 2;
            await WindowsSmoke.WithModal<FormEditDialog>(() => Click(window,"사용자 양식 설정"), async editor =>
            {
                var name = Field<TextBox>(editor,"양식 이름");
                check(name.Text == "" && !Button(editor,"지우기").IsEnabled, "A built-in form can be copied and remains protected from deletion");
                await Click(editor,"저장");
                check(editor.IsVisible && fixture.Posts == 0, "An empty form name cannot issue a save");
                var basis = Field<ComboBox>(editor,"바탕 양식");
                basis.SelectedIndex = 1;
                check(Field<TextBox>(editor,"줄 수").Text == "22", "Explicitly changing the base adopts that form's layout");
                name.Text = "점역 연습";
                Field<TextBox>(editor,"줄 수").Text = "3";
                await Click(editor,"저장");
                check(editor.IsVisible && fixture.Posts == 0, "Invalid row counts cannot issue a save");
                Field<TextBox>(editor,"줄 수").Text = "30";
                await Click(editor,"저장");
                await WaitAsync(() => !editor.IsVisible);
            });
            check(fixture.Posts == 1 && list.Items.Count == 6 && list.SelectedItem is BrailleForm { Name:"점역 연습",Layout.Lines:30 } copy
                && copy.BaseFormId == forms[1].FormId, "Copying a built-in creates and selects a distinct personal form with its base and edited layout");
            await WindowsSmoke.WithModal<FormEditDialog>(() => Click(window,"사용자 양식 설정"), async editor =>
            {
                await WindowsSmoke.WithModal<DeleteFormDialog>(() => Click(editor,"지우기"), confirm => Click(confirm,"지우기"));
                await WaitAsync(() => !editor.IsVisible);
            });
            check(fixture.Deletes == 1 && fixture.DeletedId == "user:2" && list.Items.Count == 5 &&
                list.SelectedItem is BrailleForm { Builtin:true }, "Confirming deletion removes only the selected personal form and restores a valid selection");
            list.SelectedIndex = 4;
            await Click(window,"확인");
        });
        check(form.Selected is { FormId:"user:1",Layout.PageNumbers:"짝수 페이지" }, "Confirmation returns the personal form with its saved even-side policy");
        var reloaded = await client.FormsAsync();
        check(reloaded.Length == 5 && reloaded.Single(f => !f.Builtin).Layout.PageNumbers == "짝수 페이지", "Reopening the collection retains the saved form and excludes the deleted copy");

        void Frame(Window window, int w, int h) =>
            check(window.ActualWidth == w && window.ActualHeight == h && HasIcon(window,"form-close.svg"), $"{window.Title} renders the Figma {w}x{h} frame and close icon");
        void Bounds(Window window, FrameworkElement element, int x, int y, int w, int h)
        {
            var at = element.TranslatePoint(new Point(), window);
            check(Math.Abs(at.X-x)<=1 && Math.Abs(at.Y-y)<=1 && Math.Abs(element.ActualWidth-w)<=1 && Math.Abs(element.ActualHeight-h)<=1,
                $"{window.Title} {AutomationProperties.GetName(element)} geometry {at.X},{at.Y} {element.ActualWidth}x{element.ActualHeight} matches {x},{y} {w}x{h}");
        }
    }
    private static Button Button(Window window,string label) => Descendants(window).OfType<Button>().Single(b => Equals(b.Content,label));
    private static Task Click(Window window,string label)
    {
        Button(window,label).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        return Task.CompletedTask;
    }
    private static T Field<T>(Window window,string label) where T:DependencyObject =>
        Descendants(window).OfType<T>().Single(c => AutomationProperties.GetName(c) == label);

    private static bool HasIcon(DependencyObject root, string asset) => Descendants(root).OfType<SvgViewbox>()
        .Any(icon => icon.Source?.ToString().EndsWith(asset, StringComparison.Ordinal) == true && icon.Width == 16 && icon.Height == 16 && icon.Drawings is { Bounds.IsEmpty: false });
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static async Task WaitAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5); while (!predicate() && DateTime.UtcNow < deadline) await Task.Delay(20);
        if (!predicate()) throw new InvalidOperationException("Forms fixture did not load");
    }
    private sealed class Fixture : HttpMessageHandler
    {
        internal int Posts, Patches, Deletes;
        internal string? DeletedId;
        private readonly List<JsonObject> forms = new();
        internal Fixture()
        {
            string[] names = ["일반 도서","초등 교과서","중 · 고 교과서","시험지","중학 수학 (28줄)"];
            for (var i=0;i<names.Length;i++)
            {
                var layout = new PageLayout(Lines:i==4 ? 28 : i==1 ? 22 : 26, FooterAlign:i==4 ? "right" : "center");
                forms.Add(JsonSerializer.SerializeToNode(new { formId=i==4 ? "user:1" : "builtin:"+i, name=names[i], builtin=i!=4,
                    baseFormId=i==4 ? "builtin:2" : null, layoutOptions=JobClient.LayoutBody(layout), brailleOptions=JobClient.OptionsBody(new()) })!.AsObject());
            }
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var id = Uri.UnescapeDataString(request.RequestUri!.Segments.Last());
            object result;
            if (id == "login") result = new { accessToken="fixture", refreshToken="fixture", role="ROLE_USER" };
            else if (request.Method == HttpMethod.Get) result = new { items=forms };
            else if (request.Method == HttpMethod.Delete)
            {
                Deletes++; DeletedId=id; forms.RemoveAt(forms.FindIndex(f => (string?)f["formId"]==id)); result=new {};
            }
            else
            {
                var saved = JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!.AsObject();
                saved["builtin"]=false;
                if (request.Method == HttpMethod.Post) { Posts++; saved["formId"]="user:2"; forms.Add(saved); }
                else { Patches++; saved["formId"]=id; forms[forms.FindIndex(f => (string?)f["formId"]==id)]=saved; }
                result=saved;
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent(JsonSerializer.Serialize(new { isSuccess=true,result }),Encoding.UTF8,"application/json") };
        }
    }
}
