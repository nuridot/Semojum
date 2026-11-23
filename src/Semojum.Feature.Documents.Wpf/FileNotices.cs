namespace Semojum.Desktop.Dialogs;

internal static class FileNotices
{
    internal static NoticeDialog UnsupportedFormat() => new("이 형식은 열 수 없습니다.", "PDF · HWP · HWPX · TXT · JPG · JPEG · PNG · SMJ 만 열 수 있습니다.", ["다시 선택", "취소"], error: true, width: 480, height: 178);
    internal static NoticeDialog OversizedFile() => new("파일이 100 MB 를 넘습니다.", "파일을 나누어 올린 뒤 이어 붙일 수 있습니다.", ["도움말", "취소"], error: true, width: 480, height: 178);
    internal static NoticeDialog IncompatibleFiles(string detail) => new("선택한 파일을 함께 열 수 없습니다.", detail, ["다시 선택", "취소"], error: true, width: 480, height: 178);
    internal static NoticeDialog RetryFailed(string detail) => new("실패한 쪽을 다시 변환하지 못했습니다.", detail, ["닫기"], error: true, width: 480, height: 178);
    internal static NoticeDialog MissingDrafts() => new("이 블록에는 대체 초안이 없습니다.", "그림 설명 · 표 점역 초안이 있는 블록을 고르십시오.", ["확인"], NoticeTone.Information, width: 480, height: 178);
    internal static NoticeDialog ServerSaveFailed(string detail) => new NoticeDialog("서버에 저장하지 못했습니다.", "이 컴퓨터의 .smj 파일에는 저장했습니다.", ["확인"], width: 480, height: 178) { ToolTip = detail };
    internal static NoticeDialog PartialServerSave(int total, int saved, string failures) => new($"{total}쪽 중 {saved}쪽만 서버에 저장했습니다.", $"저장하지 못한 쪽: {failures}", ["실패한 쪽 다시 저장", "취소"], width: 520, height: 178);
    internal static PdfPasswordDialog EncryptedPdf(bool wrongPassword = false) => new(wrongPassword);
    internal static NoticeDialog ServerSettingsUnavailable() => new("서버 설정을 불러오지 못했습니다.", "저장해 둔 .smj 파일은 열 수 있습니다. 변환은 연결이 돌아온 뒤에 할 수 있습니다.", ["확인"], width: 480, height: 198);
    internal static NoticeDialog ReadOnlyEdit() => new("읽기 전용 문서입니다.", "다른 이름으로 저장한 뒤 편집하십시오.", ["확인"], NoticeTone.Information, width: 480, height: 178);
    internal static NoticeDialog EngineUnavailable() => new("점역 엔진이 응답하지 않습니다.", "점자 패널만 잠깁니다. 텍스트 수정과 저장은 됩니다.", ["다시 시도", "나중에"], error: true, width: 480, height: 178);
    internal static NoticeDialog ReadOnlyDocument(bool accessDenied = false) => new(accessDenied ? "이 문서에 쓰기 권한이 없습니다." : "이 문서는 다른 창에서 열려 있습니다.", "이 창에서는 읽기 전용으로 엽니다.", ["읽기 전용으로 열기", "취소"], width: 520, height: 178);
    internal static NoticeDialog InvalidRange(int start, int end, string detail) => new("고른 범위에 내용이 없습니다.", $"시작 {start}쪽, 끝 {end}쪽입니다. {detail}", ["범위 다시 지정", "취소"], width: 480, height: 178);
    internal static NoticeDialog SaveFailed(string detail) => new("저장하지 못했습니다.", "고친 내용은 그대로 남아 있습니다.", ["다른 이름으로 저장", "다시 시도"], error: true, width: 480, height: 178) { ToolTip = detail };
    internal static NoticeDialog PartialConversion(int total, int succeeded, int[] failed)
    {
        // BE 2026-10-27 POST /retry re-queues AI-failed pages only; canceled or credit-exhausted pages are reported back as "no pages".
        return new NoticeDialog($"{total}쪽 중 {failed.Length}쪽을 변환하지 못했습니다.", $"{string.Join(" · ", failed)}쪽입니다. 나머지 {succeeded}쪽은 사용할 수 있습니다.", ["실패한 쪽 다시 변환", "그대로 열기"], height: 196);
    }
    internal static NoticeDialog UnreadableCharacters(int count) => new($"원본에서 글을 읽지 못한 자리가 {count}군데 있습니다.", "■ 로 표시했습니다. 원본을 보고 직접 입력하십시오.", ["해당 위치로 이동", "확인"], width: 520, height: 178);
    internal static NoticeDialog ConversionFailed() => new("변환하지 못했습니다.", "같은 파일로 다시 시도하거나 변환 설정을 바꾸십시오.", ["다시 시도", "변환 설정", "문의하기"], error: true, width: 520, height: 178);
    internal static NoticeDialog Disconnected(int completed)
    {
        // 이어서 변환 re-attaches to the job status stream and fetches only the pages this window has not received.
        return new NoticeDialog("서버와 연결이 끊겼습니다.", $"{completed}쪽까지 변환한 내용은 보관했습니다.", ["이어서 변환", "닫기"], width: 480, height: 178);
    }
    internal static NoticeDialog Offline(string detail) => new("서버에 연결하지 못했습니다.", "저장과 편집은 됩니다. 변환과 게시판은 연결이 돌아온 뒤에 쓸 수 있습니다.", ["다시 시도", "오프라인으로 계속"], error: true, width: 520, height: 178) { ToolTip = detail };
    internal static NoticeDialog NoCredits() => new("크레딧이 모두 소모되어 변환을 멈췄습니다.", "충전한 뒤 문서를 열면 리본 아래 알림 띠의 「이어서 처리」로 다시 시작합니다.", ["크레딧 구매", "문의하기", "닫기"], width: 560, height: 178);
    internal static NoticeDialog LoginRejected(Semojum.Core.Integration.SemojumApiException error)
    {
        bool locked = error.Code == "AUTH4006";
        var detail = locked ? error.Message : "아이디와 비밀번호를 확인한 뒤 다시 입력해 주세요.";
        var message = locked ? "로그인이 잠겼습니다." : "아이디 또는 비밀번호가 맞지 않습니다.";
        if (error.Details is { ValueKind: System.Text.Json.JsonValueKind.Object } data)
        {
            static int? Count(System.Text.Json.JsonElement data, string name) =>
                data.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetInt32(out var count) && count >= 0 ? count : null;
            var maxAttempts = Count(data, "maxAttempts"); var lockMinutes = Count(data, "lockMinutes");
            if (Count(data, "remainingAttempts") is { } attempts)
                detail = $"남은 횟수 {attempts}회입니다." + (maxAttempts.HasValue && lockMinutes.HasValue ? $" {maxAttempts}회 실패하면 {lockMinutes}분 동안 잠깁니다." : "");
            if (locked && maxAttempts is { } max && lockMinutes is { } minutes) detail = $"로그인을 {max}회 실패해 {minutes}분 동안 잠겼습니다.";
            if (data.TryGetProperty("unlockAt", out var unlock))
                detail += "\n다시 시도할 수 있는 시각: " + (unlock.ValueKind == System.Text.Json.JsonValueKind.String && DateTime.TryParse(unlock.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var at)
                    ? at.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) : unlock.ToString());
        }
        return new(message, detail, error.Code == "AUTH4006" ? ["확인"] : ["다시 입력", "비밀번호 찾기"], error: true, width: 480, height: 178);
    }
}
