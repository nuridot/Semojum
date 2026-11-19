using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Semojum.Core.Integration;

namespace Semojum.Desktop.Services;

/// <summary>Current-Windows-user DPAPI queue, with an exclusive lease so two app processes never overwrite each other.</summary>
internal sealed class WindowsAppEventStore : IAppEventStore, IDisposable
{
    private readonly string path;
    private readonly FileStream lease;
    public WindowsAppEventStore(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var candidate in Directory.EnumerateFiles(directory, "*.queue").Order())
        {
            try { lease = new FileStream(candidate + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); path = candidate; return; }
            catch (IOException) { }
        }
        path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".queue");
        lease = new FileStream(path + ".lock", FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
    }
    public EventQueue? Load()
    {
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("Event queue exceeds its limit.");
        var clear = Transform(File.ReadAllBytes(path), protect: false);
        try { return JsonSerializer.Deserialize<EventQueue>(clear, AppEvents.Json); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public void Save(EventQueue queue)
    {
        var clear = JsonSerializer.SerializeToUtf8Bytes(queue, AppEvents.Json);
        byte[] encrypted;
        try { encrypted = Transform(clear, protect: true); }
        finally { CryptographicOperations.ZeroMemory(clear); }
        if (encrypted.Length > 32 * 1024 * 1024) throw new IOException("Event queue exceeds its limit.");
        var temp = path + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { file.Write(encrypted); file.Flush(flushToDisk: true); }
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Dispose() { lease.Dispose(); try { File.Delete(path + ".lock"); } catch (IOException) { } }

    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    private static byte[] Transform(byte[] bytes, bool protect)
    {
        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) }; Blob output = default;
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            var ok = protect ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new CryptographicException(Marshal.GetLastWin32Error());
            var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally
        {
            for (var i = 0; i < input.Length; i++) Marshal.WriteByte(input.Data, i, 0);
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero)
            { for (var i = 0; i < output.Length; i++) Marshal.WriteByte(output.Data, i, 0); LocalFree(output.Data); }
        }
    }
}
