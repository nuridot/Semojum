using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Shell;
using System.Windows.Controls.Primitives;
using SharpVectors.Converters;
using Semojum.Core;
using WpfMath.Controls;

namespace Semojum.Desktop.Dialogs;



public sealed class MemoDialog : EditorDialog
{
    private readonly TextBox input;
    public string Result => input.Text;
    public MemoDialog(EditorBlock block) : base($"블록 {block.Id} · 메모")
    {
        input = new() { Text = block.Memo, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Fields.Children.Add(input); Note("메모는 현재 블록에 저장됩니다.");
    }
    public MemoDialog(string title, string text) : base(title)
    {
        input = new() { Text = text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Fields.Children.Add(input); Note("현재 패널의 블록 안 줄에 메모를 남깁니다. 내용을 비우면 메모가 삭제됩니다.");
    }
    protected override bool Apply() => true;
}


public sealed class ManualEditDialog : NoticeDialog
{
    public ManualEditDecision Decision => Choice switch { 0 => ManualEditDecision.Keep, 1 => ManualEditDecision.Overwrite, _ => ManualEditDecision.Cancel };
    public ManualEditDialog() : base("직접 고친 점자를 지우고 다시 점역하시겠습니까?",
        "유지하면 이 블록은 다시 점역되지 않습니다.", ["유지", "덮어쓰기", "취소"], width: 520, height: 178) { }
}
