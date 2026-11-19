using System.Diagnostics;
using System.Net;

namespace Semojum.Core.Integration;

/// <summary>One observed operation. Disposal is user cancellation unless a result has been supplied.</summary>
public sealed class AppEventOperation(Action<object> emit, IDictionary<string, object?> extra, Guid? retryOf = null) : IDisposable
{
    public Guid Id { get; } = Guid.NewGuid();
    private readonly long started = Stopwatch.GetTimestamp();
    private bool ended;
    public void Success() => End("success", null);
    public void Failure(Exception error) => End("failure", ErrorCode(error));
    public void Cancel() => End("cancelled", "user_cancelled");
    public void Abandon() => ended = true; // no result was observed, e.g. application shutdown
    private void End(string result, string? error)
    {
        if (ended) return; ended = true;
        extra["operation_id"] = Id; extra["retry_of_operation_id"] = retryOf; extra["result"] = result;
        extra["error_code"] = error; extra["duration_ms"] = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        emit(extra);
    }
    public void Dispose() { if (!ended) Cancel(); }
    public static string ErrorCode(Exception error) => error switch
    {
        FileNotFoundException or DirectoryNotFoundException => "file_not_found",
        UnauthorizedAccessException => "permission_denied",
        InvalidDataException => "invalid_document",
        NotSupportedException => "unsupported_format",
        TimeoutException or OperationCanceledException => "timeout",
        HttpRequestException => "network_unavailable",
        SemojumApiException { Code: "JOB4018" or "JOB4019" } => "password_required",
        SemojumApiException { Status: HttpStatusCode.Conflict } => "version_conflict",
        SemojumApiException => "server_error",
        IOException io when (io.HResult & 0xFFFF) is 39 or 112 => "disk_full",
        _ => "unknown_error"
    };
}
