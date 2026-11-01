namespace Hwpx2Hwp.Core.Model;

/// <summary>
/// 일괄 실행 1회의 결과 요약. Pending 은 취소·엔진 시작 실패로 처리하지 않은 파일 수.
/// Notices 는 엔진 시작 안내(F6·T1.3), LogPath 는 이번 실행의 로그 파일. D-4.
/// </summary>
public sealed record BatchReport(
    int Succeeded,
    int Failed,
    int Pending,
    IReadOnlyList<BatchFailure> Failures,
    string LogPath,
    IReadOnlyList<string> Notices,
    bool Cancelled);

/// <summary>실패한 파일 1건(원본 경로·이유·진단용 원문).</summary>
public sealed record BatchFailure(string SourcePath, FailureReason Reason, string? Detail);
