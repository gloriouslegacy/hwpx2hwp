namespace Hwpx2Hwp.Core.Model;

/// <summary>변환 경로(원본 확장자 → 대상 확장자). 확장자는 점 포함 소문자(예: ".hwpx"). D-4, V3.</summary>
public sealed record ConversionRoute(string SourceExtension, string TargetExtension)
{
    /// <summary>hwpx → hwp. 현재 유일한 경로.</summary>
    public static ConversionRoute HwpxToHwp { get; } = new(".hwpx", ".hwp");
}
