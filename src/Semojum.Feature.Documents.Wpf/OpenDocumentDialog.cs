using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Win32;
using Semojum.Core;
using Semojum.Core.Integration;
using Semojum.Feature.Settings;

namespace Semojum.Desktop.Dialogs;

public sealed class OpenDocumentDialog : ConversionFormDialog
{
    internal ComboBox Forms { get; } = new() { DisplayMemberPath = nameof(BrailleForm.Name) };
    internal TextBox FileNames { get; } = new() { IsReadOnly = true, IsHitTestVisible = false, Focusable = false };
    private readonly Pair korean = new("한글", "정자(1급)", "약자(2급)");
    private readonly Pair rule = new("점역 규정", "한글", "영어");
    private readonly Pair mixed = new("한영 혼용 규정", "한국어", "영어");
    private readonly Button start;
    private readonly ProofModelSelector proof;
    private BrailleOptions current;
    private PageLayout layout;
    private readonly bool serverSettingsAvailable;
    private bool usesProof;
    public string[] Paths { get; private set; } = [];
    public string? FormId => (Forms.SelectedItem as BrailleForm)?.FormId;
    public DocumentSession Settings => new() {
        Layout = usesProof && proof.Model is { } model ? layout with { ProofModel = model.Code, AdvancedAi = model.Code != "MINERU" }
            : layout with { ProofModel = null, AdvancedAi = false },
        Options = current with { KoreanGrade1 = korean.FirstSelected, EnglishDocument = !rule.FirstSelected, MixedRule = mixed.FirstSelected ? "korean" : "english" } };

    public OpenDocumentDialog(BrailleForm[] forms, AccountDefaults defaults, bool serverSettingsAvailable = true, ProofPricing? pricing = null)
        : base("점역 옵션", 600, 714)
    {
        this.serverSettingsAvailable = serverSettingsAvailable;
        current = defaults.Options; layout = new(PageNumbers: defaults.PageNumberLine == "every" ? "모든 페이지" : "홀수 페이지");
        var rows = new StackPanel();
        Fields.Children.Add(Card(rows, new(16, 12, 16, 12)));
        FormRow(rows, "파일", Entry(FileNames, "올릴 파일"), 162);
        FileNames.Padding = new(10, 0, 11, 0);
        FileNames.Foreground = EditorModalDesign.Color("#8A8A8A");
        FormRow(rows, "점역 양식", SelectField(Forms, "점역 양식"), 162);
        FormRow(rows, "한글", korean, 162); FormRow(rows, "점역 규정", rule, 162); FormRow(rows, "한영 혼용 규정", mixed, 162);
        proof = new(pricing, layout.ProofModel); Section("교열 모델").Children.Add(proof);
        Forms.ItemsSource = forms; FormPickerDialog.Attach(Forms, this);
        void SetOptions(BrailleOptions options)
        {
            current = options; korean.FirstSelected = options.KoreanGrade1;
            rule.FirstSelected = !options.EnglishDocument; mixed.FirstSelected = options.MixedRule != "english";
        }
        Forms.SelectionChanged += (_, _) => { if (Forms.SelectedItem is BrailleForm form) { SetOptions(form.Options); layout = form.Layout; } };
        Forms.SelectedItem = forms.FirstOrDefault(f => f.FormId == defaults.FormId) ?? forms.FirstOrDefault(f => f.FormId == "builtin:GENERAL_BOOK") ?? forms.FirstOrDefault();
        SetOptions(defaults.Options with { KoreanGrade1 = false });
        CancelButton();
        start = FooterButton("변환 시작", () =>
        {
            try
            {
                if (Paths.Length == 0) throw new ArgumentException("파일을 선택해 주세요.");
                string[] oversized = IsLocal ? [] : Paths.Where(p => new FileInfo(p).Length > JobClient.MaximumUploadBytes).Select(p => Path.GetFileName(p)).ToArray();
                if (oversized.Length > 0)
                {
                    // The limit is per file; the Figma notice stays generic, so the status line names the oversized files.
                    Status.Text = "100 MB를 넘는 파일: " + string.Join(", ", oversized);
                    var notice = FileNotices.OversizedFile(); notice.Owner = this; notice.ShowDialog();
                    if (notice.Choice == 0) Semojum.Desktop.Services.CommonWindowServices.ShowHelp(this, "파일 열기");
                    return Task.CompletedTask;
                }
                // Several images also share one 100 MB total (BE 2026-11-02, JOB4009): the same notice, and the status line gives the total.
                long[] sizes = IsLocal ? [] : Paths.Select(p => new FileInfo(p).Length).ToArray();
                if (JobClient.ExceedsImageTotal(sizes))
                {
                    Status.Text = $"이미지 {Paths.Length}장 합계 {Math.Ceiling(sizes.Sum() / (1024d * 1024d)):0} MB · 여러 장은 합계 100 MB까지 올릴 수 있습니다.";
                    var notice = FileNotices.OversizedFile(); notice.Owner = this; notice.ShowDialog();
                    if (notice.Choice == 0) Semojum.Desktop.Services.CommonWindowServices.ShowHelp(this, "파일 열기");
                    return Task.CompletedTask;
                }
                Settings.Layout.Validate(); DialogResult = true;
            }
            catch (Exception ex) { Status.Text = ex.Message; }
            return Task.CompletedTask;
        }, true);
        Loaded += (_, _) => ReadPdfCount(Paths);
        if (!serverSettingsAvailable) Status.Text = "서버 설정을 불러오지 못했습니다. 저장된 .smj 작업은 열 수 있습니다. 새 변환은 연결을 확인한 뒤 다시 시도해 주세요.";
    }

    private bool IsLocal => Paths.Length == 1 && Path.GetExtension(Paths[0]).Equals(".smj", StringComparison.OrdinalIgnoreCase);
    private static readonly string[] Supported = [".pdf", ".hwp", ".hwpx", ".txt", ".jpg", ".jpeg", ".png", ".smj"];
    internal static string[]? SelectPaths(Window owner)
    {
        var picker = new OpenFileDialog { Multiselect = true, Filter = "지원 파일|*.pdf;*.hwp;*.hwpx;*.txt;*.jpg;*.jpeg;*.png;*.smj|누리닷 작업|*.smj|문서|*.pdf;*.hwp;*.hwpx;*.txt|이미지|*.jpg;*.jpeg;*.png" };
        while (picker.ShowDialog(owner) == true)
        {
            try { ValidatePaths(picker.FileNames); return picker.FileNames; }
            catch (ArgumentException ex)
            {
                if (picker.FileNames.All(p => Supported.Contains(Path.GetExtension(p).ToLowerInvariant())))
                {
                    if (FileNotices.IncompatibleFiles(ex.Message).ChooseFor(owner) == 0) continue;
                    return null;
                }
                var notice = FileNotices.UnsupportedFormat(); notice.Owner = owner; notice.ShowDialog();
                if (notice.Choice != 0) return null;
            }
        }
        return null;
    }

    internal static void ValidatePaths(string[] paths)
    {
        var images = new[] { ".jpg", ".jpeg", ".png" };
        if (paths.Length == 0 || paths.Any(p => !Supported.Contains(Path.GetExtension(p).ToLowerInvariant()))) throw new ArgumentException("지원하는 문서 또는 이미지 파일을 선택해 주세요.");
        if (paths.Length > 1 && paths.Any(p => !images.Contains(Path.GetExtension(p).ToLowerInvariant()))) throw new ArgumentException("문서는 한 개씩, 이미지는 여러 개를 함께 선택할 수 있습니다.");
        if (paths.Length > 1) UploadFileReader.ValidateUniqueFileNames(paths);
    }
    internal void SetPaths(string[] paths)
    {
        ValidatePaths(paths);
        Paths = paths.ToArray();
        var local = IsLocal;
        var textFile = Paths.Length == 1 && Path.GetExtension(Paths[0]).Equals(".txt", StringComparison.OrdinalIgnoreCase);
        usesProof = !local && !textFile && serverSettingsAvailable;
        int? count = Path.GetExtension(Paths[0]).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" ? Paths.Length : null;
        SetFileNames(count);
        proof.Configure(count, local || textFile ? "0 크레딧" : null, usesProof);
        start.Content = local ? "열기" : "변환 시작";
        start.IsEnabled = local || serverSettingsAvailable;
        Forms.IsEnabled = korean.IsEnabled = rule.IsEnabled = mixed.IsEnabled = !local && serverSettingsAvailable;
        start.ToolTip = local ? "작업 파일의 기존 설정을 그대로 엽니다." : "PDF·HWP·HWPX·이미지는 서버로 전송하며 성공한 쪽에 사용량이 차감됩니다. TXT는 로컬 점역합니다.";
        Status.Text = !serverSettingsAvailable && !local ? "서버 설정을 불러오지 못했습니다. 새 변환은 연결을 확인한 뒤 다시 시도해 주세요." : "";
        if (IsLoaded) ReadPdfCount(Paths);
    }

    private void SetFileNames(int? pages)
    {
        FileNames.Text = string.Join("; ", Paths.Select(p => DisplayFileName.Normalize(Path.GetFileName(p))))
            + (pages.HasValue ? $" · {pages}쪽" : "");
        FileNames.ToolTip = FileNames.Text;
    }

    private async void ReadPdfCount(string[] selected)
    {
        if (selected.Length != 1 || !Path.GetExtension(selected[0]).Equals(".pdf", StringComparison.OrdinalIgnoreCase) || !File.Exists(selected[0])) return;
        try
        {
            if (new FileInfo(selected[0]).Length > JobClient.MaximumUploadBytes) return;
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(Path.GetFullPath(selected[0])).AsTask(Lifetime.Token);
            var pdf = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file).AsTask(Lifetime.Token);
            if (!ReferenceEquals(Paths, selected) || Lifetime.IsCancellationRequested) return;
            int count = checked((int)pdf.PageCount);
            SetFileNames(count); proof.Configure(count, enabled: usesProof);
        }
        catch (OperationCanceledException) { }
        // Windows.Data.Pdf can return unmapped HRESULTs for encrypted or malformed files.
        // Metadata is optional. The upload path handles the actual document or requests a password.
        catch (Exception ex) when (ex is not OutOfMemoryException) { System.Diagnostics.Debug.WriteLine(ex.Message); }
    }
}
