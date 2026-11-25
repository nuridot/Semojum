using System.IO;
using Semojum.Core.Integration;

namespace Semojum.Desktop.Services;

internal static class EngineRuntime
{
    public static BrailleSidecar Create()
    {
        var python = Environment.GetEnvironmentVariable("SEMOJUM_ENGINE_PYTHON");
        if (!string.IsNullOrWhiteSpace(python))
            return new BrailleSidecar(new SidecarCommand(python, ["-u", Path.Combine(AppContext.BaseDirectory, "engine-adapter", "engine-entry.py")]));
        var directory = Path.Combine(AppContext.BaseDirectory, "engine");
        var engine = Path.Combine(directory, PortableUpdate.EngineLauncher);
        var legacy = Path.Combine(directory, "semojum-engine.exe");
        return new BrailleSidecar(new SidecarCommand(!File.Exists(engine) && File.Exists(legacy) ? legacy : engine, []));
    }
}
