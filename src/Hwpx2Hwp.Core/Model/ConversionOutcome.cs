namespace Hwpx2Hwp.Core.Model;

/// <summary>파일 단위 변환 결과. 실패는 예외가 아니라 이 값으로 돌려준다. D-4.</summary>
public sealed record ConversionOutcome(bool Succeeded, FailureReason? FailureReason, string? Detail)
{
    /// <summary>성공 결과.</summary>
    public static ConversionOutcome Success() => new(true, null, null);

    /// <summary>실패 결과. Detail 은 진단용 원문(선택).</summary>
    public static ConversionOutcome Failure(FailureReason reason, string? detail = null) => new(false, reason, detail);
}
