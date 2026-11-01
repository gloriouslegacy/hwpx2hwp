namespace Hwpx2Hwp.Core.Model;

/// <summary>파일 단위 실패 이유. D-4.</summary>
public enum FailureReason
{
    InputMissing,
    Encrypted,
    OpenFailed,
    SaveFailed,
    EngineCrashed,
    Unexpected,
}
