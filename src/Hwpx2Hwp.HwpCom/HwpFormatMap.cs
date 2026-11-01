namespace Hwpx2Hwp.HwpCom;

/// <summary>확장자(ConversionRoute 의 Source/Target) → 한/글 Open·SaveAs 형식 문자열. D-6, V3.</summary>
internal static class HwpFormatMap
{
    /// <summary>".hwpx"→"HWPX", ".hwp"→"HWP"(대소문자 무시). 모르는 확장자는 ArgumentException.</summary>
    internal static string ToHwpFormat(string extension)
    {
        if (string.Equals(extension, ".hwpx", StringComparison.OrdinalIgnoreCase))
        {
            return "HWPX";
        }

        if (string.Equals(extension, ".hwp", StringComparison.OrdinalIgnoreCase))
        {
            return "HWP";
        }

        throw new ArgumentException($"한/글 형식으로 바꿀 수 없는 확장자: {extension}", nameof(extension));
    }
}
