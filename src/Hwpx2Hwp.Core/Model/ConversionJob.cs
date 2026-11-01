namespace Hwpx2Hwp.Core.Model;

/// <summary>엔진에 넘기는 변환 작업 1건(입력·확정된 출력 경로·경로). D-4.</summary>
public sealed record ConversionJob(InputItem Input, string OutputPath, ConversionRoute Route);
