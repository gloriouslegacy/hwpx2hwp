namespace Hwpx2Hwp.Core.Engine;

/// <summary>
/// 엔진 시작 결과. 미설치면 Available=false + 안내 문구(T1.3),
/// 보안 모듈 없음/등록 실패면 Notices 에 안내 1건(F6). D-3.
/// </summary>
public sealed record EngineStart(bool Available, string? UnavailableMessage, IReadOnlyList<string> Notices);
