using System.Globalization;
using Hwpx2Hwp.Core.Model;

namespace Hwpx2Hwp.Core.Logging;

/// <summary>FailureReason 의 화면·로그 문구를 만드는 유일한 곳(UI·로그·CLI 공용). D-4.</summary>
public static class FailureReasonText
{
    /// <summary>실패 이유 문구. 정의되지 않은 값도 예외 없이 문구를 돌려준다.</summary>
    public static string Get(FailureReason reason) => reason switch
    {
        FailureReason.InputMissing => "원본 파일이 없음",
        FailureReason.Encrypted => "암호가 걸린 문서라 변환할 수 없음",
        FailureReason.OpenFailed => "문서를 열 수 없음",
        FailureReason.SaveFailed => "변환한 문서를 저장하지 못함",
        FailureReason.EngineCrashed => "한/글이 비정상 종료됨",
        FailureReason.Unexpected => "예상하지 못한 오류",
        _ => "알 수 없는 이유(" + ((int)reason).ToString(CultureInfo.InvariantCulture) + ")",
    };
}
