using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Semojum.Core;

namespace Semojum.Desktop.Services;

public static class RawBraillePrinter
{
    // RAW sends the engine's BRF bytes unchanged. Paper/duplex are device settings, not text escape codes.
    public static Task PrintAsync(Window owner, string brf, string title)
    {
        if (brf.Any(c => c > 127)) throw new ArgumentException("BRF ASCII 파일만 점자 프린터로 보낼 수 있습니다.");
        if (Dialogs.NoticeDialog.Choose(owner, "BRF를 직접 지원하는 점자 프린터를 선택해 주세요.", "프린터에서 용지 크기·양면 설정을 먼저 확인해야 합니다. 일반 프린터에는 점자로 인쇄되지 않습니다.", ["프린터 선택", "취소"]) != 0) return Task.CompletedTask;
        var faces = DocumentExport.BrfPages(brf).Length;
        if (faces < 1) throw new ArgumentException("인쇄할 점자 면이 없습니다.");
        var dialog = new Dialogs.BraillePrintDialog(faces, () => Task.Run(InstalledQueues),
            (name, first, last, copies) => SendRangeAsync(brf, title, name, first, last, copies, Send)) { Owner = owner };
        dialog.ShowDialog();
        return Task.CompletedTask;
    }

    private static (string[] Queues, string? Default) InstalledQueues()
    {
        using var server = new System.Printing.LocalPrintServer();
        using var queues = server.GetPrintQueues([System.Printing.EnumeratedPrintQueueTypes.Local, System.Printing.EnumeratedPrintQueueTypes.Connections]);
        var names = new List<string>();
        foreach (var queue in queues) { using (queue) names.Add(queue.FullName); }
        string? preferred = null;
        try { using var queue = server.DefaultPrintQueue; preferred = queue?.FullName; }
        catch (System.Printing.PrintSystemException) { }
        return (names.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToArray(), preferred);
    }

    internal static Task SendRangeAsync(string brf, string title, string name, int first, int last, int copies,
        Action<string, string, byte[], int> send)
    {
        if (copies is < 1 or > 100) throw new ArgumentException("인쇄 부수는 1–100부로 입력해 주세요.");
        if (brf.Any(c => c > 127)) throw new ArgumentException("BRF ASCII 파일만 점자 프린터로 보낼 수 있습니다.");
        var faces = DocumentExport.BrfPages(brf).Length;
        if (first < 1 || last < first || last > faces) throw new ArgumentException("인쇄할 점자 면의 범위를 확인해 주세요.");
        var selected = first == 1 && last == faces ? brf : DocumentExport.SelectBraille(brf, new(ExportScope.BraillePages, first, last));
        var bytes = Encoding.ASCII.GetBytes(selected);
        return Task.Run(() => send(name, title, bytes, copies));
    }
    private static void Send(string name, string title, byte[] bytes, int copies)
    {
        if (!OpenPrinter(name, out var printer, IntPtr.Zero)) throw new Win32Exception();
        var started = false;
        try
        {
            var info = new DocInfo { Name = title, DataType = "RAW" };
            if (StartDocPrinter(printer, 1, info) == 0) throw new Win32Exception();
            started = true;
            for (var copy = 0; copy < copies; copy++)
            {
                if (!StartPagePrinter(printer)) throw new Win32Exception();
                if (!WritePrinter(printer, bytes, bytes.Length, out var written) || written != bytes.Length) throw new Win32Exception("인쇄 데이터 전체를 전달하지 못했습니다.");
                if (!EndPagePrinter(printer)) throw new Win32Exception();
            }
            if (!EndDocPrinter(printer)) throw new Win32Exception();
            started = false;
        }
        finally { if (started) AbortPrinter(printer); ClosePrinter(printer); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DocInfo { [MarshalAs(UnmanagedType.LPWStr)] public string Name = ""; [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile; [MarshalAs(UnmanagedType.LPWStr)] public string DataType = "RAW"; }
    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool OpenPrinter(string name, out IntPtr handle, IntPtr defaults);
    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint StartDocPrinter(IntPtr handle, int level, [In] DocInfo info);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool StartPagePrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool WritePrinter(IntPtr handle, byte[] data, int length, out int written);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndPagePrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndDocPrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool AbortPrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool ClosePrinter(IntPtr handle);
}
