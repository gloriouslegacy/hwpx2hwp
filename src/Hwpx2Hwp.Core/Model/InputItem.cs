namespace Hwpx2Hwp.Core.Model;

/// <summary>변환 입력 1건. BaseFolder 는 끌어다 놓은 폴더(상대 구조 기준, F3), 파일 입력이면 null. D-4.</summary>
public sealed record InputItem(string SourcePath, string? BaseFolder);
