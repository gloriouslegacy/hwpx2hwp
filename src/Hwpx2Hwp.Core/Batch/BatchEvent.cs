using Hwpx2Hwp.Core.Model;

namespace Hwpx2Hwp.Core.Batch;

/// <summary>
/// 파일 1건의 상태 변화(Running → Succeeded|Failed). JobNumber 는 1부터(로그 #0001 과 같다).
/// OutputPath·Outcome 은 끝난 이벤트에만 있다(출력 경로를 정하기 전에 실패하면 OutputPath 는 null).
/// Completed 는 이 이벤트 시점까지 끝난 파일 수, Total 은 입력 수. F4.
/// </summary>
public sealed record BatchEvent(
    int JobNumber,
    InputItem Input,
    JobState State,
    string? OutputPath,
    ConversionOutcome? Outcome,
    int Completed,
    int Total);

/// <summary>실행 옵션. OutputFolder null 이면 원본과 같은 폴더(F3). EngineName 은 로그 머리 줄에 쓴다.</summary>
public sealed record BatchOptions(ConversionRoute Route, string? OutputFolder, string EngineName);
